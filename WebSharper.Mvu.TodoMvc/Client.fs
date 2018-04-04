[<WebSharper.JavaScript>]
module TodoMvc.Client

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.UI.Notation
open WebSharper.Mvu

module Model =

    type Key = int

    type TodoEntry =
        {
            Id : Key
            Task : string
            IsCompleted : bool
            Editing : option<string>
        }

        static member Key e = e.Id

        static member New key task =
            {
                Id = key
                Task = task
                IsCompleted = false
                Editing = None
            }

    type TodoList =
        {
            NewTask : string
            Todos : list<TodoEntry>
            NextKey : Key
        }

        static member Empty =
            {
                NewTask = ""
                Todos = []
                NextKey = 0
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

    module Entry =

        [<NamedUnionCases "type">]
        type Message =
            | Remove
            | StartEdit
            | Edit of text: string
            | CommitEdit
            | CancelEdit
            | SetCompleted of completed: bool

        let Update msg (t: Model.TodoEntry) : option<Model.TodoEntry> =
            match msg with
            | Remove ->
                None
            | StartEdit ->
                Some { t with Editing = t.Editing |> Option.orElse (Some t.Task) }
            | Edit value ->
                Some { t with Editing = Some value }
            | CommitEdit ->
                Some { t with
                        Task = t.Editing |> Option.defaultValue t.Task
                        Editing = None }
            | CancelEdit ->
                Some { t with Editing = None }
            | SetCompleted value ->
                Some { t with IsCompleted = value }

    let private updateAllEntries f (model: Model.TodoList) =
        { model with Todos = f model.Todos }

    let private updateEntry key f (model: Model.TodoList) =
        model |> updateAllEntries (List.choose (fun t -> if t.Id = key then f t else Some t))

    [<NamedUnionCases "type">]
    type Message =
        | EditNewTask of text: string
        | AddEntry
        | ClearCompleted
        | SetAllCompleted of completed: bool
        | EntryMessage of key: Model.Key * message: Entry.Message

    let Update msg (model: Model.TodoList) =
        match msg with
        | EditNewTask value ->
            { model with NewTask = value }
        | AddEntry ->
            { model with
                NewTask = ""
                Todos = model.Todos @ [Model.TodoEntry.New model.NextKey model.NewTask]
                NextKey = model.NextKey + 1 }
        | ClearCompleted ->
            model |> updateAllEntries (List.filter (fun t -> not t.IsCompleted))
        | SetAllCompleted c ->
            model |> updateAllEntries (List.map (fun t -> { t with IsCompleted = c }))
        | EntryMessage (key, msg) ->
            model |> updateEntry key (Entry.Update msg)

module Render =

    type MasterTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>

    let TodoEntry dispatch (todo: View<Model.TodoEntry>) =
        MasterTemplate.TODO()
            .Label(text todo.V.Task)
            .CssAttrs(
                Attr.ClassPred "completed" todo.V.IsCompleted,
                Attr.ClassPred "editing" todo.V.Editing.IsSome,
                Attr.ClassPred "hidden" (
                    match Route.location.V, todo.V.IsCompleted with
                    | Route.Completed, false -> true
                    | Route.Active, true -> true
                    | _ -> false
                )
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

    let TodoList dispatch (state: View<Model.TodoList>) =
        MasterTemplate()
            .TODOs(V(state.V.Todos).DocSeqCached(Model.TodoEntry.Key, fun key todo ->
                let entryDispatch msg = dispatch (Update.EntryMessage (key, msg))
                TodoEntry entryDispatch todo
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
    App.Create Model.TodoList.Empty Update.Update Render.TodoList
    |> App.WithLocalStorage "todolist"
    |> App.WithRemoteDev (RemoteDev.Options(hostname = "localhost", port = 8000))
    |> App.Run
