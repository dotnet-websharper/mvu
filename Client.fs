[<WebSharper.JavaScript>]
module todomvc.my.Client

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.UI.Notation

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
                    { t with Editing = t.Editing |> Option.orElse (Some t.Task) }
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
                    V(todo.V.Editing |> Option.defaultValue ""),
                    fun text -> dispatch (Update.Entry.Edit text)
                )
                .EditBlur(fun _ -> dispatch Update.Entry.CommitEdit)
                .EditKeyup(fun e ->
                    match e.Event.Key with
                    | "Enter" -> dispatch Update.Entry.CommitEdit
                    | "Escape" -> dispatch Update.Entry.CancelEdit
                    | _ -> ()
                )
                .IsCompleted(
                    V todo.V.IsCompleted,
                    fun x -> dispatch (Update.Entry.SetCompleted x)
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
                    V(match state.V.Todos with
                        | [] -> false
                        | l -> l |> List.forall (fun t -> t.IsCompleted)),
                    fun c -> dispatch (Update.SetAllCompleted c)
                )
                .Task(
                    V state.V.NewTask,
                    fun text -> dispatch (Update.EditNewTask text)
                )
                .Edit(fun e ->
                    if e.Event.Key = "Enter" then
                        dispatch Update.AddEntry
                        e.Event.PreventDefault()
                )
                .ItemsLeft(
                    V(match List.length state.V.Todos with
                        | 1 -> "1 item left"
                        | n -> string n + " items left")
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
