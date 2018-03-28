namespace todomvc.my

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI

/// The Model-View-Update-like system
[<JavaScript>]
module App =

    type Dispatch<'Message> = 'Message -> unit

    type App<'Message, 'Model, 'Rendered> =
        internal {
            Init : unit -> unit
            Var : Var<'Model>
            View : View<'Model>
            Update : 'Message -> 'Model -> 'Model
            Render : Dispatch<'Message> -> View<'Model> -> 'Rendered
        }

    let Create (initModel: 'Model)
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

    // Inline needed because of the generic macro on Serializer.Typed
    [<Inline>]
    let WithLocalStorage (key: string) (app: App<_, 'Model, _>) =
        let serializer = Serializer.Typed<'Model>
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

    [<Inline>]
    let WithRemoteDev options (app: App<'Message, 'Model, _>) =
        WithRemoteDev' Serializer.Typed<'Message> Serializer.Typed<'Model> options app
