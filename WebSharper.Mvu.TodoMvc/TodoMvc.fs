[<WebSharper.JavaScript>]
module TodoMvc.Client

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.Mvu

/// This module defines the types of our application's model.
module Model =

    /// The unique identifier of a Todo entry.
    type Key = int

    /// The model for a Todo entry.
    type Entry =
        {
            Id : Key
            Task : string
            IsCompleted : bool
            Editing : option<string>
        }

        static member Key (e: Entry) = e.Id

        static member New (key: Key) (task: string) =
            {
                Id = key
                Task = task
                IsCompleted = false
                Editing = None
            }

    /// The model for the full TodoList application.
    type TodoList =
        {
            NewTask : string
            Entries : list<Entry>
            NextKey : Key
        }

        static member Empty =
            {
                NewTask = ""
                Entries = []
                NextKey = 0
            }


/// This module defines the URL routing of our application.
module Route =
    open WebSharper.Sitelets

    /// Our application has three URL endpoints.
    type EndPoint =
        | [<EndPoint "/">] All
        | [<EndPoint "/active">] Active
        | [<EndPoint "/completed">] Completed

    /// The router defines the mapping between the URL and the route value.
    let router = Router.Infer<EndPoint>()

    /// The installed router is a Var whose value is synchronized with the current URL.
    let location = Router.InstallHash EndPoint.All router


/// This module defines the updates that can be applied to the application's model.
module Update =

    /// Updates for a specific Todo entry.
    module Entry =

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
        let Update (msg: Message) (e: Model.Entry) : option<Model.Entry> =
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

    [<NamedUnionCases "type">]
    type Message =
        | EditNewTask of text: string
        | AddEntry
        | ClearCompleted
        | SetAllCompleted of completed: bool
        | EntryMessage of key: Model.Key * message: Entry.Message

    /// Defines how the Todo list is updated based on a message.
    let TodoList (msg: Message) (model: Model.TodoList) =
        match msg with
        | EditNewTask value ->
            { model with NewTask = value }
        | AddEntry ->
            { model with
                NewTask = ""
                Entries = model.Entries @ [Model.Entry.New model.NextKey model.NewTask]
                NextKey = model.NextKey + 1 }
        | ClearCompleted ->
            { model with Entries = List.filter (fun e -> not e.IsCompleted) model.Entries }
        | SetAllCompleted c ->
            { model with Entries = List.map (fun e -> { e with IsCompleted = c }) model.Entries }
        | EntryMessage (key, msg) ->
            let updateEntry (e: Model.Entry) =
                if e.Id = key then Entry.Update msg e else Some e
            { model with Entries = List.choose updateEntry model.Entries }


/// This module defines the rendering of our application.
module Render =

    /// Parses the index.html file and provides types to fill it with dynamic content.
    type MasterTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>

    /// Render a given Todo entry.
    let Entry (dispatch: Update.Entry.Message -> unit) (entry: View<Model.Entry>) =
        MasterTemplate.Entry()
            .Label(text entry.V.Task)
            .CssAttrs(
                Attr.ClassPred "completed" entry.V.IsCompleted,
                Attr.ClassPred "editing" entry.V.Editing.IsSome,
                Attr.ClassPred "hidden" (
                    match Route.location.V, entry.V.IsCompleted with
                    | Route.Completed, false -> true
                    | Route.Active, true -> true
                    | _ -> false
                )
            )
            .EditingTask(
                V(entry.V.Editing |> Option.defaultValue ""),
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
                V entry.V.IsCompleted,
                fun x -> dispatch (Update.Entry.SetCompleted x)
            )
            .Remove(fun _ -> dispatch Update.Entry.Remove)
            .StartEdit(fun _ -> dispatch Update.Entry.StartEdit)
            .Doc()

    /// Render the whole application.
    let TodoList (dispatch: Update.Message -> unit) (state: View<Model.TodoList>) =
        MasterTemplate()
            .Entries(V(state.V.Entries).DocSeqCached(Model.Entry.Key, fun key entry ->
                let entryDispatch msg = dispatch (Update.EntryMessage (key, msg))
                Entry entryDispatch entry
            ))
            .ClearCompleted(fun _ -> dispatch Update.ClearCompleted)
            .IsCompleted(
                V(match state.V.Entries with
                    | [] -> false
                    | l -> l |> List.forall (fun e -> e.IsCompleted)),
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
                V(match List.length state.V.Entries with
                    | 1 -> "1 item left"
                    | n -> string n + " items left")
            )
            .CssFilterAll(Attr.ClassPred "selected" (Route.location.V = Route.EndPoint.All))
            .CssFilterActive(Attr.ClassPred "selected" (Route.location.V = Route.EndPoint.Active))
            .CssFilterCompleted(Attr.ClassPred "selected" (Route.location.V = Route.EndPoint.Completed))
            .Bind()

/// The entry point of our application, called on page load.
[<SPAEntryPoint>]
let Main () =
    App.Create Model.TodoList.Empty Update.TodoList Render.TodoList
    |> App.WithLocalStorage "todolist"
    |> App.WithRemoteDev (RemoteDev.Options(hostname = "localhost", port = 8000))
    |> App.Run
