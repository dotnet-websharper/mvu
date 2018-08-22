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
        Init : Dispatch<'Message> -> unit
        Var : Var<'Model>
        View : View<'Model>
        Update : Dispatch<'Message> -> 'Message -> 'Model -> option<'Model>
        Render : Dispatch<'Message> -> View<'Model> -> 'Rendered
    }

/// An action to take as a result of the Update function.
[<JavaScript>]
type Action<'Message, 'Model> =
    | DoNothing
    | SetModel of 'Model
    | Command of (Dispatch<'Message> -> unit)
    | CommandAsync of (Dispatch<'Message> -> Async<unit>)
    | CombinedAction of list<Action<'Message, 'Model>>

    /// Combine two actions.
    static member (+) (a1: Action<'Message, 'Model>, a2: Action<'Message, 'Model>) =
        match a1, a2 with
        | a, DoNothing | DoNothing, a -> a
        | CombinedAction l1, CombinedAction l2 -> CombinedAction (l1 @ l2)
        | CombinedAction l1, a2 -> CombinedAction (l1 @ [a2])
        | a1, CombinedAction l2 -> CombinedAction (a1 :: l2)
        | a1, a2 -> CombinedAction [a1; a2]

    static member DispatchAsync<'T> (toMessage: 'T -> 'Message) (action: Async<'T>) : Action<'Message, 'Model> =
        CommandAsync (fun dispatch -> async {
            let! res = action
            dispatch (toMessage res)
        })

[<Require(typeof<Resources.PagerCss>)>]
[<JavaScript>]
type Page<'Message, 'Model> =
    internal {
        Render: Pager<'Message, 'Model> -> Dispatch<'Message> -> View<'Model> -> Elt
        KeepInDom: bool
        UsesTransition: bool
    }

    /// <summary>
    /// Create a reactive page.
    /// </summary>
    /// <param name="key">Get the identifier of the current endpoint. A new instance of the page is only created for different values of the key.</param>
    /// <param name="render">Render the page itself.</param>
    /// <param name="attrs">Attributes to add to the wrapping div.</param>
    /// <param name="keepInDom">If true, don't remove the page from the DOM when hidden.</param>
    /// <param name="usesTransition">Pass true if this page uses CSS transitions to appear and disappear.</param>
    static member Reactive
        (
            key: 'EndPointArgs -> 'K,
            render: 'K -> Dispatch<'Message> -> View<'Model> -> #Doc,
            ?attrs: seq<Attr>,
            ?keepInDom: bool,
            ?usesTransition: bool
        ) =
        let dic = Dictionary()
        let getOrRender (route: 'EndPointArgs) (pager: Pager<'Message, 'Model>) (dispatch: Dispatch<'Message>) (model: View<'Model>) =
            let k = key route
            match dic.TryGetValue k with
            | true, (var, doc) ->
                Var.Set var route
                doc
            | false, _ ->
                let var = Var.Create route
                let doc =
                    Elt.div [
                        attr.``class`` "ws-page"
                        (match attrs with Some attrs -> Attr.Concat attrs | None -> Attr.Empty)
                        on.transitionEnd (fun el ev -> pager.RemoveIfNeeded el)
                    ] [render k dispatch model]
                dic.[k] <- (var, doc)
                doc
        fun ep ->
            {
                Render = getOrRender ep
                KeepInDom = defaultArg keepInDom false
                UsesTransition = defaultArg usesTransition false
            } : Page<'Message, 'Model>

    /// <summary>
    /// Create a reactive page. A new instance of the page is created for different values of the endpoint args.
    /// </summary>
    /// <param name="render">Render the page itself.</param>
    /// <param name="attrs">Attributes to add to the wrapping div.</param>
    /// <param name="keepInDom">If true, don't remove the page from the DOM when hidden.</param>
    /// <param name="usesTransition">Pass true if this page uses CSS transitions to appear and disappear.</param>
    static member Create(render, ?attrs, ?keepInDom, ?usesTransition) : 'EndPointArgs -> _ =
        Page<'Message, 'Model>.Reactive(id, render, ?attrs = attrs, ?keepInDom = keepInDom, ?usesTransition = usesTransition)

    /// <summary>
    /// Create a reactive page. A single instance of the page is (lazily) created.
    /// </summary>
    /// <param name="render">Render the page itself.</param>
    /// <param name="attrs">Attributes to add to the wrapping div.</param>
    /// <param name="keepInDom">If true, don't remove the page from the DOM when hidden.</param>
    /// <param name="usesTransition">Pass true if this page uses CSS transitions to appear and disappear.</param>
    static member Single(render, ?attrs, ?keepInDom, ?usesTransition) =
        Page<'Message, 'Model>.Reactive(ignore, (fun () -> render), ?attrs = attrs, ?keepInDom = keepInDom, ?usesTransition = usesTransition)

and [<JavaScript>] internal Pager<'Message, 'Model>(render: 'Model -> Page<'Message, 'Model>, dispatch: Dispatch<'Message>, model: View<'Model>) as this =
    let mutable toRemove = None : option<Elt>

    let rec container : WebSharper.UI.Client.EltUpdater =
        let elt =
            Elt.div [
                attr.``class`` "ws-page-container"
                on.viewUpdate model (fun el r ->
                    let page = render r
                    let elt = page.Render this dispatch model
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
                        if page.UsesTransition then
                            toRemove.Dom?dataset?wsRemoving <- "true"
                        else
                            el.RemoveChild toRemove.Dom |> ignore
                        container.RemoveUpdated toRemove
                    if not (el.Contains domElt) then
                        domElt.SetAttribute("aria-hidden", "true")
                        el.AppendChild domElt |> ignore
                        container.AddUpdated elt
                        JS.RequestAnimationFrame (fun _ -> domElt.RemoveAttribute("aria-hidden")) |> ignore
                    toRemove <- if page.KeepInDom then None else Some elt
                )
            ] []
        elt.ToUpdater()

    member __.RemoveIfNeeded(elt: Dom.Element) =
        if elt?dataset?wsRemoving = "true" then
            elt?dataset?wsRemoving <- "false"
            container.Dom.RemoveChild elt |> ignore

    member __.Doc = container :> Doc

/// Bring together the Model-View-Update system and augment it with extra capabilities.
[<JavaScript>]
module App =

    let private create initModel update render =
        let var = Var.Create initModel
        {
            Init = ignore
            Var = var
            View = var.View
            Update = update
            Render = render
        }

    /// <summary>
    /// Create an MVU application.
    /// </summary>
    /// <param name="initModel">The initial value of the model.</param>
    /// <param name="update">Computes the new model on every message.</param>
    /// <param name="render">Renders the application based on a reactive view of the model.</param>
    let CreateSimple
            (initModel: 'Model)
            (update: 'Message -> 'Model -> 'Model)
            (render: Dispatch<'Message> -> View<'Model> -> 'Rendered) =
        let update _ msg mdl =
            Some (update msg mdl)
        create initModel update render

    let rec private applyAction dispatch = function
        | DoNothing -> None
        | SetModel mdl -> Some mdl
        | Command f -> f dispatch; None
        | CommandAsync f -> Async.Start (f dispatch); None
        | CombinedAction actions ->
            (None, actions)
            ||> List.fold (fun newModel action ->
                applyAction dispatch action
                |> Option.orElse newModel
            )

    /// <summary>
    /// Create an MVU application.
    /// </summary>
    /// <param name="initModel">The initial value of the model.</param>
    /// <param name="update">Computes the new model and/or dispatches commands on every message.</param>
    /// <param name="render">Renders the application based on a reactive view of the model.</param>
    let Create (initModel: 'Model)
            (update: 'Message -> 'Model -> Action<'Message, 'Model>)
            (render: Dispatch<'Message> -> View<'Model> -> 'Rendered) =
        let update dispatch msg mdl =
            update msg mdl |> applyAction dispatch
        create initModel update render

    /// <summary>
    /// Create an MVU application using paging.
    /// </summary>
    /// <param name="initModel">The initial value of the model.</param>
    /// <param name="update">Computes the new model and/or dispatches commands on every message.</param>
    /// <param name="render">Renders the application based on a reactive view of the model.</param>
    let CreatePaged
            (initModel: 'Model)
            (update: 'Message -> 'Model -> Action<'Message, 'Model>)
            (render: 'Model -> Page<'Message, 'Model>) =
        let render (dispatch: Dispatch<'Message>) (view: View<'Model>) =
            Pager<'Message, 'Model>(render, dispatch, view).Doc
        Create initModel update render

    /// <summary>
    /// Create an MVU application using paging.
    /// </summary>
    /// <param name="initModel">The initial value of the model.</param>
    /// <param name="update">Computes the new model on every message.</param>
    /// <param name="render">Renders the application based on a reactive view of the model.</param>
    let CreateSimplePaged
            (initModel: 'Model)
            (update: 'Message -> 'Model -> 'Model)
            (render: 'Model -> Page<'Message, 'Model>) =
        let update msg mdl =
            SetModel (update msg mdl)
        CreatePaged initModel update render

    let private withRouting<'Route, 'Message, 'Model, 'Rendered when 'Route : equality>
            (lensedRouter: Var<'Route>)
            (router: WebSharper.Sitelets.Router<'Route>)
            (getRoute: 'Model -> 'Route)
            (app: App<'Message, 'Model, 'Rendered>) =
        { app with
            Init = fun dispatch ->
                app.Init dispatch
                let defaultRoute = getRoute app.Var.Value
                Router.InstallHashInto lensedRouter defaultRoute router }

    /// <summary>
    /// Add URL hash routing to an application's model.
    /// Note: due to a limitation, you cannot currently pipe into this function.
    /// </summary>
    /// <param name="router">The URL router.</param>
    /// <param name="getRoute">Where the current endpoint is stored in the model. Must be a record field access.</param>
    /// <param name="app">The application.</param>
    [<Macro(typeof<Macros.WithRouting>)>]
    let WithRouting<'Route, 'Message, 'Model, 'Rendered when 'Route : equality>
            (router: WebSharper.Sitelets.Router<'Route>)
            (getRoute: 'Model -> 'Route)
            (app: App<'Message, 'Model, 'Rendered>) =
        withRouting (app.Var.LensAuto getRoute) router getRoute app

    let private withLocalStorage
            (serializer: Serializer<'Model>)
            (key: string)
            (app: App<_, 'Model, _>) =
        let init dispatch =
            match JS.Window.LocalStorage.GetItem(key) with
            | null -> ()
            | v -> 
                try app.Var.Set <| serializer.Decode (JSON.Parse v)
                with exn ->
                    Console.Error("Error deserializing state from local storage", exn)
            app.Init dispatch
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
        withLocalStorage Serializer.Typed<'Model> key app

    /// Run the given action on startup.
    let WithInitAction (action: Action<'Message, 'Model>) (app: App<'Message, 'Model, _>) =
        let init dispatch =
            app.Init dispatch
            applyAction dispatch action
            |> Option.iter app.Var.Set
        { app with Init = init }

    /// Run the application.
    let Run (app: App<_, _, _>) =
        let rec dispatch msg = app.Var.UpdateMaybe (app.Update dispatch msg)
        app.Init dispatch
        app.Render dispatch app.View

    let private withRemoteDev
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
        let update dispatch msg model =
            let newModel = app.Update dispatch msg model
            match newModel with
            | Some newModel ->
                rdev.send(
                    msgSerializer.Encode msg,
                    modelSerializer.Encode newModel
                )
            | None -> ()
            newModel
        let init dispatch =
            app.Init dispatch
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
        withRemoteDev Serializer.Typed<'Message> Serializer.Typed<'Model> options app
