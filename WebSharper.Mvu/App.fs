namespace WebSharper.Mvu

#nowarn "40" // let rec container

open System.Collections.Generic
open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client

/// A function that dispatches a message to the update function.
type Dispatch<'Message> = 'Message -> unit

/// An MVU application.
[<JavaScript>]
type App<'Message, 'Model, 'Rendered> =
    internal {
        Init : unit -> unit
        Var : Var<'Model>
        View : View<'Model>
        Update : 'Message -> 'Model -> 'Model
        Render : Dispatch<'Message> -> View<'Model> -> 'Rendered
    }

[<Require(typeof<Resources.PagerCss>)>]
[<JavaScript>]
[<Sealed>]
type Page<'Message, 'Model> internal (render: Dispatch<'Message> -> View<'Model> -> Elt, isRoot: bool, keepInDom: bool) =

    member __.IsRoot = isRoot

    member __.KeepInDom = keepInDom

    member __.Render(disp, mdl) =
        render disp mdl

    static member Reactive
        (
            key: 'EndPointArgs -> 'K,
            render: 'K -> Dispatch<'Message> -> View<'Model> -> #Doc,
            ?isRoot: bool,
            ?keepInDom: bool
        ) =
        let dic = Dictionary()
        let getOrRender (route: 'EndPointArgs) (dispatch: Dispatch<'Message>) (model: View<'Model>) =
            let k = key route
            match dic.TryGetValue k with
            | true, (var, doc) ->
                Var.Set var route
                doc
            | false, _ ->
                let var = Var.Create route
                let doc = div [attr.``class`` "ws-page"] [render k dispatch model]
                dic.[k] <- (var, doc)
                doc
        fun ep -> Page<'Message, 'Model>(getOrRender ep, defaultArg isRoot false, defaultArg keepInDom true)

    static member Create(render, ?isRoot, ?keepInDom) =
        Page<'Message, 'Model>.Reactive(id, render, ?isRoot = isRoot, ?keepInDom = keepInDom)

    static member Single(render, ?isRoot, ?keepInDom) =
        Page<'Message, 'Model>.Reactive(ignore, (fun () -> render), ?isRoot = isRoot, ?keepInDom = keepInDom)

[<JavaScript>]
[<Sealed>]
type internal Pager<'Message, 'Model> (route: Var<'Model>, render: 'Model -> Page<'Message, 'Model>, attrs: seq<Attr>, dispatch: Dispatch<'Message>, model: View<'Model>) =
    let mutable toRemove = None : option<Elt>

    let rec container : WebSharper.UI.Client.EltUpdater =
        let elt =
            div [
                attr.``class`` "ws-page-container"
                on.viewUpdate route.View (fun el r ->
                    let page = render r
                    let elt = page.Render(dispatch, model)
                    let domElt = elt.Dom
                    let children = el.ChildNodes
                    for i = 0 to children.Length - 1 do
                        if children.[i] !==. domElt then
                            (children.[i] :?> Dom.Element).SetAttribute("aria-hidden", "true")
                            |> ignore
                    domElt.RemoveAttribute("aria-hidden")
                    match toRemove with
                    | None -> ()
                    | Some toRemove ->
                        el.RemoveChild toRemove.Dom |> ignore
                        container.RemoveUpdated toRemove
                    if not (el.Contains domElt) then
                        el.AppendChild domElt |> ignore
                        container.AddUpdated elt
                    toRemove <- if page.KeepInDom then None else Some elt
                )
                Attr.Concat attrs
            ] []
        elt.ToUpdater()

    member __.Doc = container :> Doc

/// Bring together the Model-View-Update system and augment it with extra capabilities.
[<JavaScript>]
module App =

    /// <summary>
    /// Create an MVU application based on an initial model, an update function
    /// and a render function.
    /// </summary>
    /// <param name="initModel">The initial value of the model.</param>
    /// <param name="update">Computes the new model on every message.</param>
    /// <param name="render">Renders the application based on a reactive view of the model.</param>
    let Create
            (initModel: 'Model)
            (update: 'Message -> 'Model -> 'Model)
            (render: Dispatch<'Message> -> View<'Model> -> 'Rendered) =
        let var = Var.Create initModel
        {
            Init = ignore
            Var = var
            View = var.View
            Update = update
            Render = render
        }

    let CreatePaged
            (initModel: 'Model)
            (update: 'Message -> 'Model -> 'Model)
            (render: 'Model -> Page<'Message, 'Model>) =
        let var = Var.Create initModel
        let render (dispatch: Dispatch<'Message>) (view: View<'Model>) =
            let pager = Pager(var, render, [], dispatch, view)
            pager.Doc
        {
            Init = ignore
            Var = var
            View = var.View
            Update = update
            Render = render
        }

    let private WithLocalStorage'
            (serializer: Serializer<'Model>)
            (key: string)
            (app: App<_, 'Model, _>) =
        let init() =
            app.Init()
            match JS.Window.LocalStorage.GetItem(key) with
            | null -> ()
            | v -> 
                try app.Var.Set <| serializer.Decode (JSON.Parse v)
                with exn ->
                    Console.Error("Error deserializing state from local storage", exn)
        let view =
            app.View.Map(fun v ->
                JS.Window.LocalStorage.SetItem(key, JSON.Stringify (serializer.Encode v))
                v
            )
        { app with View = view; Init = init }

    /// <summary>
    /// Add Local Storage capability to the application.
    /// On startup, load the model from local storage at the given key,
    /// or keep the initial model if there is nothing stored yet.
    /// On every update, store the model in local storage.
    /// </summary>
    /// <param name="key">The local storage key</param>
    /// <param name="app">The application</param>
    [<Inline>]
    let WithLocalStorage key (app: App<_, 'Model, _>) =
        WithLocalStorage' Serializer.Typed<'Model> key app

    /// Run the application.
    let Run (app: App<_, _, _>) =
        let dispatch msg = Var.Update app.Var (app.Update msg)
        app.Init()
        app.Render dispatch app.View

    let private WithRemoteDev'
            (msgSerializer: Serializer<'Message>)
            (modelSerializer: Serializer<'Model>)
            (options: RemoteDev.Options)
            (app: App<'Message, 'Model, _>) =
        let rdev = RemoteDev.ConnectViaExtension(options)
        // Not sure why this is necessary :/
        let decode (m: obj) =
            match m with
            | :? string as s -> modelSerializer.Decode (JSON.Parse s)
            | m -> modelSerializer.Decode m
        rdev.subscribe(fun msg ->
            if msg.``type`` = RemoteDev.MsgTypes.Dispatch then
                match msg.payload.``type`` with
                | RemoteDev.PayloadTypes.JumpToAction
                | RemoteDev.PayloadTypes.JumpToState ->
                    let state = decode (RemoteDev.ExtractState msg)
                    app.Var.Set state
                | RemoteDev.PayloadTypes.ImportState ->
                    let state = msg.payload.nextLiftedState.computedStates |> Array.last
                    let state = decode state?state
                    app.Var.Set state
                    rdev.send(null, msg.payload.nextLiftedState)
                | _ -> ()
        )
        |> ignore
        let update msg model =
            let newModel = app.Update msg model
            rdev.send(
                msgSerializer.Encode msg,
                modelSerializer.Encode newModel
            )
            newModel
        let init() =
            app.Init()
            app.View |> View.Get (fun st ->
                rdev.init(modelSerializer.Encode st)
            )
        { app with Init = init; Update = update }

    /// <summary>
    /// Add RemoteDev capability to the application.
    /// Allows inspecting the model's history and time-travel debugging.
    /// </summary>
    /// <param name="options">The RemoteDev options</param>
    /// <param name="app">The application</param>
    [<Inline>]
    let WithRemoteDev options (app: App<'Message, 'Model, _>) =
        WithRemoteDev' Serializer.Typed<'Message> Serializer.Typed<'Model> options app
