[<WebSharper.JavaScript>]
module TodoMvc.Client

open WebSharper
open WebSharper.JavaScript
open WebSharper.Sitelets.InferRouter
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.Mvu

/// Parses the index.html file and provides types to fill it with dynamic content.
type MasterTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>

/// Our application has three URL endpoints.
type EndPoint =
    | [<EndPoint "/">] All
    | [<EndPoint "/active">] Active
    | [<EndPoint "/completed">] Completed

/// This module defines the model, the update and the view for a single entry.
module Entry =

    /// The unique identifier of a Todo entry.
    type Key = int

    /// The model for a Todo entry.
    type Model =
        {
            Id : Key
            Task : string
            IsCompleted : bool
            Editing : option<string>
        }

    let KeyOf (e: Model) = e.Id

    let New (key: Key) (task: string) =
        {
            Id = key
            Task = task
            IsCompleted = false
            Editing = None
        }

    [<NamedUnionCases "type">]
    type Message =
        | Remove
        | StartEdit
        | Edit of text: string
        | CommitEdit
        | CancelEdit
        | SetCompleted of completed: bool

    /// Defines how a given Todo entry is updated based on a message.
    /// Returns Some to update the entry, or None to delete it.
    let Update (msg: Message) (e: Model) : option<Model> =
        match msg with
        | Remove ->
            None
        | StartEdit ->
            Some { e with Editing = e.Editing |> Option.orElse (Some e.Task) }
        | Edit value ->
            Some { e with Editing = Some value }
        | CommitEdit ->
            Some { e with
                    Task = e.Editing |> Option.defaultValue e.Task
                    Editing = None }
        | CancelEdit ->
            Some { e with Editing = None }
        | SetCompleted value ->
            Some { e with IsCompleted = value }

    /// Render a given Todo entry.
    let Render (dispatch: Message -> unit) (endpoint: View<EndPoint>) (entry: View<Model>) =
        MasterTemplate.Entry()
            .Label(text entry.V.Task)
            .CssAttrs(
                Attr.ClassPred "completed" entry.V.IsCompleted,
                Attr.ClassPred "editing" entry.V.Editing.IsSome,
                Attr.ClassPred "hidden" (
                    match endpoint.V, entry.V.IsCompleted with
                    | EndPoint.Completed, false -> true
                    | EndPoint.Active, true -> true
                    | _ -> false
                )
            )
            .EditingTask(
                V(entry.V.Editing |> Option.defaultValue ""),
                fun text -> dispatch (Message.Edit text)
            )
            .EditBlur(fun _ -> dispatch Message.CommitEdit)
            .EditKeyup(fun e ->
                match e.Event.Key with
                | "Enter" -> dispatch Message.CommitEdit
                | "Escape" -> dispatch Message.CancelEdit
                | _ -> ()
            )
            .IsCompleted(
                V entry.V.IsCompleted,
                fun x -> dispatch (Message.SetCompleted x)
            )
            .Remove(fun _ -> dispatch Message.Remove)
            .StartEdit(fun _ -> dispatch Message.StartEdit)
            .Doc()

/// This module defines the model, the update and the view for a full todo list.
module TodoList =    

    /// The model for the full TodoList application.
    type Model =
        {
            EndPoint : EndPoint
            NewTask : string
            Entries : list<Entry.Model>
            NextKey : Entry.Key
        }

        static member Empty =
            {
                EndPoint = All
                NewTask = ""
                Entries = []
                NextKey = 0
            }

    [<NamedUnionCases "type">]
    type Message =
        | EditNewTask of text: string
        | AddEntry
        | ClearCompleted
        | SetAllCompleted of completed: bool
        | EntryMessage of key: Entry.Key * message: Entry.Message

    /// Defines how the Todo list is updated based on a message.
    let Update (msg: Message) (model: Model) =
        match msg with
        | EditNewTask value ->
            { model with NewTask = value }
        | AddEntry ->
            { model with
                NewTask = ""
                Entries = model.Entries @ [Entry.New model.NextKey model.NewTask]
                NextKey = model.NextKey + 1 }
        | ClearCompleted ->
            { model with Entries = List.filter (fun e -> not e.IsCompleted) model.Entries }
        | SetAllCompleted c ->
            { model with Entries = List.map (fun e -> { e with IsCompleted = c }) model.Entries }
        | EntryMessage (key, msg) ->
            let updateEntry (e: Entry.Model) =
                if Entry.KeyOf e = key then Entry.Update msg e else Some e
            { model with Entries = List.choose updateEntry model.Entries }

    /// Render the whole application.
    let Render (dispatch: Message -> unit) (state: View<Model>) =
        let countNotCompleted =
            V(state.V.Entries
                |> List.filter (fun e -> not e.IsCompleted)
                |> List.length)
        MasterTemplate()
            .Entries(
                V(state.V.Entries).DocSeqCached(Entry.KeyOf, fun key (entry: View<Entry.Model>) ->
                    let entryDispatch msg = dispatch (EntryMessage (key, msg))
                    Entry.Render entryDispatch (V state.V.EndPoint) entry
                )
            )
            .ClearCompleted(fun _ -> dispatch Message.ClearCompleted)
            .IsCompleted(
                V(countNotCompleted.V = 0),
                fun c -> dispatch (Message.SetAllCompleted c)
            )
            .Task(
                V state.V.NewTask,
                fun text -> dispatch (Message.EditNewTask text)
            )
            .Edit(fun e ->
                if e.Event.Key = "Enter" then
                    dispatch Message.AddEntry
                    e.Event.PreventDefault()
            )
            .ItemsLeft(
                V(match countNotCompleted.V with
                    | 1 -> "1 item left"
                    | n -> string n + " items left")
            )
            .CssFilterAll(Attr.ClassPred "selected" (state.V.EndPoint = EndPoint.All))
            .CssFilterActive(Attr.ClassPred "selected" (state.V.EndPoint = EndPoint.Active))
            .CssFilterCompleted(Attr.ClassPred "selected" (state.V.EndPoint = EndPoint.Completed))
            .Bind()

/// The entry point of our application, called on page load.
[<SPAEntryPoint>]
let Main () =
    let app = App.CreateSimple TodoList.Model.Empty TodoList.Update TodoList.Render
    App.WithRouting (Router.Infer()) (fun (model: TodoList.Model) -> model.EndPoint) app
    |> App.WithLocalStorage "todolist"
    |> App.WithRemoteDev (RemoteDev.Options(hostname = "localhost", port = 8000))
    |> App.Run
