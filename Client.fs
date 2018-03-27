[<WebSharper.JavaScript>]
module todomvc.my.Client

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.UI.Notation

//// Library additions

/// The Model-View-Update-like system
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
        { app with View = view }

    let Run (app: App<_, _, _>) =
        let dispatch msg = Var.Update app.Var (app.Update msg)
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

module Var =

    /// Create a Var from a View and a setter function.
    let Make (view: View<'T>) (set: 'T -> unit) =
        let id = "test"
        let mutable current = As null
        let view = view.Map(fun x -> current <- x; x)
        { new Var<'T>() with
            member this.View = view
            member this.Id = id
            member this.Set(x) = set x
            member this.UpdateMaybe(f) =
                view |> View.Get (fun x ->
                    match f x with
                    | None -> ()
                    | Some x -> set x
                )
            member this.Update(f) =
                view |> View.Get (f >> set)
            member this.SetFinal(x) = set x
            member this.Get() = current
        }

/// The application model
module Model =

    type TodoEntry =
        {
            Id : Key
            Task : string
            IsCompleted : bool
            Editing : option<string>
        }

        static member Key e = e.Id

        static member New task =
            {
                Id = Key.Fresh()
                Task = task
                IsCompleted = false
                Editing = None
            }

    type TodoList =
        {
            NewTask : string
            Todos : list<TodoEntry>
        }

        static member Empty =
            {
                NewTask = ""
                Todos = [] 
            }

/// Dummy implementation of a hypothetical RPC server that would save entries in a database.
module Server =

    let Save (entries: Model.TodoEntry[]) =
        async { return Console.Log(entries) }

module Route =
    open WebSharper.Sitelets

    type EndPoint =
        | [<EndPoint "/">] All
        | [<EndPoint "/active">] Active
        | [<EndPoint "/completed">] Completed

    let router = Router.Infer<EndPoint>()
    let location = Router.InstallHash EndPoint.All router

module Update =

    let private updateAllEntries f (model: Model.TodoList) =
        { model with Todos = f model.Todos }

    let private updateEntry key f (model: Model.TodoList) =
        model |> updateAllEntries (List.map (fun t -> if t.Id = key then f t else t))

    module Entry =

        [<NamedUnionCases "type">]
        type Message =
            | Remove
            | StartEdit
            | Edit of text: string
            | CommitEdit
            | CancelEdit
            | SetCompleted of completed: bool

        let Update key msg (model: Model.TodoList) =
            match msg with
            | Remove ->
                model |> updateAllEntries (List.filter (fun t -> t.Id <> key))
            | StartEdit ->
                model |> updateEntry key (fun t ->
                    { t with
                        Editing = t.Editing |> Option.orElse (Some t.Task)
                    }
                )
            | Edit value ->
                model |> updateEntry key (fun t -> { t with Editing = Some value })
            | CommitEdit ->
                model |> updateEntry key (fun t ->
                    { t with
                        Task = t.Editing |> Option.defaultValue t.Task
                        Editing = None
                    }
                )
            | CancelEdit ->
                model |> updateEntry key (fun t -> { t with Editing = None })
            | SetCompleted value ->
                model |> updateEntry key (fun t -> { t with IsCompleted = value })

    [<NamedUnionCases "type">]
    type Message =
        | EditNewTask of text: string
        | AddEntry
        | ClearCompleted
        | SetAllCompleted of completed: bool
        | EntryMessage of key: Key * message: Entry.Message

    let Update msg (model: Model.TodoList) =
        match msg with
        | EditNewTask value ->
            { model with NewTask = value }
        | AddEntry ->
            { model with
                NewTask = ""
                Todos = model.Todos @ [Model.TodoEntry.New model.NewTask]
            }
        | ClearCompleted ->
            model |> updateAllEntries (List.filter (fun t -> not t.IsCompleted))
        | SetAllCompleted c ->
            model |> updateAllEntries (List.map (fun t -> { t with IsCompleted = c }))
        | EntryMessage (key, msg) ->
            model |> Entry.Update key msg

module Render =

    type MasterTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>

    module TodoEntry =
        
        let Render dispatch (todo: View<Model.TodoEntry>) =
            MasterTemplate.TODO()
                .Label(text todo.V.Task)
                .CssAttrs(
                    [
                        Attr.ClassPred "completed" todo.V.IsCompleted
                        Attr.ClassPred "editing" todo.V.Editing.IsSome
                        Attr.ClassPred "hidden" (
                            match Route.location.V, todo.V.IsCompleted with
                            | Route.Completed, false -> true
                            | Route.Active, true -> true
                            | _ -> false
                        )
                    ]
                )
                .EditingTask(
                    Var.Make
                        (V(todo.V.Editing |> Option.defaultValue ""))
                        (fun text -> dispatch (Update.Entry.Edit text))
                )
                .EditBlur(fun _ -> dispatch Update.Entry.CommitEdit)
                .EditKeyup(fun e ->
                    match e.Event.Key with
                    | "Enter" -> dispatch Update.Entry.CommitEdit
                    | "Escape" -> dispatch Update.Entry.CancelEdit
                    | _ -> ()
                )
                .IsCompleted(
                    Var.Make
                        (V todo.V.IsCompleted)
                        (fun x -> dispatch (Update.Entry.SetCompleted x))
                )
                .Remove(fun _ -> dispatch Update.Entry.Remove)
                .StartEdit(fun _ -> dispatch Update.Entry.StartEdit)
                .Doc()

    module TodoList =

        let Render dispatch (state: View<Model.TodoList>) =
            MasterTemplate()
                .TODOs(V(state.V.Todos).DocSeqCached(Model.TodoEntry.Key, fun key todo ->
                    let entryDispatch msg = dispatch (Update.EntryMessage (key, msg))
                    TodoEntry.Render entryDispatch todo
                ))
                .ClearCompleted(fun _ -> dispatch Update.ClearCompleted)
                .IsCompleted(
                    Var.Make
                        (V(
                            let todos = state.V.Todos
                            not (List.isEmpty todos)
                            && todos |> List.forall (fun t -> t.IsCompleted)
                        ))
                        (fun c -> dispatch (Update.SetAllCompleted c))
                )
                .Task(
                    Var.Make
                        (V state.V.NewTask)
                        (fun text -> dispatch (Update.EditNewTask text))
                )
                .Edit(fun e ->
                    if e.Event.Key = "Enter" then
                        dispatch Update.AddEntry
                        e.Event.PreventDefault()
                )
                .CssFilterAll(Attr.ClassPred "selected" (Route.location.V = Route.EndPoint.All))
                .CssFilterActive(Attr.ClassPred "selected" (Route.location.V = Route.EndPoint.Active))
                .CssFilterCompleted(Attr.ClassPred "selected" (Route.location.V = Route.EndPoint.Completed))
                .Bind()

[<SPAEntryPoint>]
let Main () =
    App.Create Model.TodoList.Empty Update.Update Render.TodoList.Render
    |> App.WithLocalStorage "todolist"
    |> App.WithRemoteDev (RemoteDev.Options(hostname = "localhost", port = 8000))
    |> App.Run
