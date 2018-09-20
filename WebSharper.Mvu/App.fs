namespace WebSharper.Mvu

#nowarn "40" // let rec container

open System.Collections.Generic
open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client

type Dispatch<'Message> = 'Message -> unit

[<JavaScript>]
type App<'Message, 'Model, 'Rendered> =
    internal {
        Init : Dispatch<'Message> -> unit
        Var : Var<'Model>
        View : View<'Model>
        Update : Dispatch<'Message> -> 'Message -> 'Model -> option<'Model>
        Render : Dispatch<'Message> -> View<'Model> -> 'Rendered
    }

[<JavaScript>]
type Action<'Message, 'Model> =
    | DoNothing
    | SetModel of 'Model
    | UpdateModel of ('Model -> 'Model)
    | Command of (Dispatch<'Message> -> unit)
    | CommandAsync of (Dispatch<'Message> -> Async<unit>)
    | CombinedAction of list<Action<'Message, 'Model>>

    static member (+) (a1: Action<'Message, 'Model>, a2: Action<'Message, 'Model>) =
        match a1, a2 with
        | a, DoNothing | DoNothing, a -> a
        | CombinedAction l1, CombinedAction l2 -> CombinedAction (l1 @ l2)
        | CombinedAction l1, a2 -> CombinedAction (l1 @ [a2])
        | a1, CombinedAction l2 -> CombinedAction (a1 :: l2)
        | a1, a2 -> CombinedAction [a1; a2]

[<AutoOpen; JavaScript>]
module Action =
    /// Run the given asynchronous job then dispatch a message based on its result.
    let DispatchAsync (toMessage: 'T -> 'Message) (action: Async<'T>) : Action<'Message, 'Model> =
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

    static member Reactive
        (
            key: 'EndPointArgs -> 'K,
            render: 'K -> Dispatch<'Message> -> View<'Model> -> Doc,
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

    static member Create(render, ?attrs, ?keepInDom, ?usesTransition) : 'EndPointArgs -> _ =
        Page<'Message, 'Model>.Reactive(id, render, ?attrs = attrs, ?keepInDom = keepInDom, ?usesTransition = usesTransition)

    static member Single(render, ?attrs, ?keepInDom, ?usesTransition) =
        Page<'Message, 'Model>.Reactive((fun () -> ()), (fun () -> render), ?attrs = attrs, ?keepInDom = keepInDom, ?usesTransition = usesTransition)

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
                    | Some toRemove when toRemove !==. elt ->
                        if page.UsesTransition then
                            toRemove.Dom?dataset?wsRemoving <- "true"
                        else
                            el.RemoveChild toRemove.Dom |> ignore
                        container.RemoveUpdated toRemove
                    | _ -> ()
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

    let CreateSimple<'Message, 'Model, 'Rendered>
            (initModel: 'Model)
            (update: 'Message -> 'Model -> 'Model)
            (render: Dispatch<'Message> -> View<'Model> -> 'Rendered) =
        let update _ msg mdl =
            Some (update msg mdl)
        create initModel update render

    let rec private applyAction dispatch oldModel = function
        | DoNothing -> None
        | SetModel mdl -> Some mdl
        | UpdateModel f -> Some (f oldModel)
        | Command f -> f dispatch; None
        | CommandAsync f -> Async.Start (f dispatch); None
        | CombinedAction actions ->
            (None, actions)
            ||> List.fold (fun newModel action ->
                applyAction dispatch (defaultArg newModel oldModel) action
                |> Option.orElse newModel
            )

    let Create<'Message, 'Model, 'Rendered>
            (initModel: 'Model)
            (update: 'Message -> 'Model -> Action<'Message, 'Model>)
            (render: Dispatch<'Message> -> View<'Model> -> 'Rendered) =
        let update dispatch msg mdl =
            update msg mdl |> applyAction dispatch mdl
        create initModel update render

    let CreatePaged<'Message, 'Model>
            (initModel: 'Model)
            (update: 'Message -> 'Model -> Action<'Message, 'Model>)
            (render: 'Model -> Page<'Message, 'Model>) =
        let render (dispatch: Dispatch<'Message>) (view: View<'Model>) =
            Pager<'Message, 'Model>(render, dispatch, view).Doc
        Create initModel update render

    let CreateSimplePaged<'Message, 'Model>
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

    [<Macro(typeof<Macros.WithRouting>)>]
    let WithRouting<'Route, 'Message, 'Model, 'Rendered when 'Route : equality>
            (router: WebSharper.Sitelets.Router<'Route>)
            (getRoute: 'Model -> 'Route)
            (app: App<'Message, 'Model, 'Rendered>) =
        withRouting (app.Var.LensAuto getRoute) router getRoute app

    let WithCustomRouting<'Route, 'Message, 'Model, 'Rendered when 'Route : equality>
            (router: WebSharper.Sitelets.Router<'Route>)
            (getRoute: 'Model -> 'Route)
            (setRoute: 'Route -> 'Model -> 'Model)
            (app: App<'Message, 'Model, 'Rendered>) =
        withRouting (app.Var.Lens getRoute (fun m r -> setRoute r m)) router getRoute app

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

    [<Inline>]
    let WithLocalStorage key (app: App<_, 'Model, _>) =
        withLocalStorage Serializer.Typed<'Model> key app

    let WithInitAction (action: Action<'Message, 'Model>) (app: App<'Message, 'Model, _>) =
        let init dispatch =
            app.Init dispatch
            applyAction dispatch app.Var.Value action
            |> Option.iter app.Var.Set
        { app with Init = init }

    let WithInitMessage (message: 'Message) (app: App<'Message, 'Model, 'Rendered>) =
        WithInitAction (Command (fun dispatch -> dispatch message)) app

    let Run (app: App<_, _, _>) =
        let rec dispatch msg = app.Var.UpdateMaybe (app.Update dispatch msg)
        app.Init dispatch
        app.Render dispatch app.View

    let WithLog
            (log: 'Message -> 'Model -> unit)
            (app: App<'Message, 'Model, _>) =
        let update dispatch msg model =
            let newModel = app.Update dispatch msg model
            log msg (defaultArg newModel model)
            newModel
        { app with Update = update }

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

    [<Inline>]
    let WithRemoteDev options (app: App<'Message, 'Model, _>) =
        withRemoteDev Serializer.Typed<'Message> Serializer.Typed<'Model> options app
