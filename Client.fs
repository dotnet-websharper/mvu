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
module MVU =

    type Dispatch<'Update> = 'Update -> unit

    let Run (initModel: 'Model)
            (dispatch: 'Update -> 'Model -> 'Model)
            (render: Dispatch<'Update> -> View<'Model> -> 'Rendered) =
        let var = Var.Create initModel
        let dispatch msg = var.Update(dispatch msg)
        render dispatch var.View

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
        }

        static member Key e = e.Id

        static member New task =
            {
                Id = Key.Fresh()
                Task = task
                IsCompleted = false
            }

    type TodoList =
        {
            Todos : list<TodoEntry>
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

    type Message =
        | RemoveEntry of Key
        | Edit of Key * string
        | SetCompleted of Key * bool
        | ClearCompleted
        | SetAllCompleted of bool
        | New of string

    let Dispatch msg (model: Model.TodoList) =
        match msg with
        | RemoveEntry key ->
            { model with Todos = model.Todos |> List.filter (fun e -> e.Id <> key) }
        | Edit (key, value) ->
            let updateEntry (t: Model.TodoEntry) =
                if t.Id = key then { t with Task = value } else t
            { model with Todos = List.map updateEntry model.Todos }
        | SetCompleted (key, value) ->
            let updateEntry (t: Model.TodoEntry) =
                if t.Id = key then { t with IsCompleted = value } else t
            { model with Todos = List.map updateEntry model.Todos }
        | ClearCompleted ->
            { model with Todos = model.Todos |> List.filter (fun e -> not e.IsCompleted) }
        | SetAllCompleted c ->
            { model with Todos = model.Todos |> List.map (fun e -> { e with IsCompleted = c }) }
        | New task ->
            { model with Todos = Model.TodoEntry.New task :: model.Todos }

module Render =

    type MasterTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>

    module TodoEntry =

        type private InternalState =
            {
                Draft : string
                IsEditing : bool
            }

        let Render dispatch (key: Key) (todo: View<Model.TodoEntry>) =
            let state = Var.Create { Draft = ""; IsEditing = false }
            MasterTemplate.TODO()
                .Label(text todo.V.Task)
                .CssAttrs(
                    [
                        Attr.ClassPred "completed" todo.V.IsCompleted
                        Attr.ClassPred "editing" state.V.IsEditing
                        Attr.ClassPred "hidden" (
                            match Route.location.V, todo.V.IsCompleted with
                            | Route.Completed, false -> true
                            | Route.Active, true -> true
                            | _ -> false
                        )
                    ]
                )
                .Task(Lens state.V.Draft)
                .IsCompleted(
                    Var.Make
                        (V todo.V.IsCompleted)
                        (fun x -> dispatch (Update.SetCompleted (key, x)))
                )
                .Remove(fun _ -> dispatch (Update.RemoveEntry key))
                .ToggleEdit(fun _ ->
                    todo |> View.Get (fun todo ->
                        state.Update(fun s -> { s with Draft = todo.Task; IsEditing = true })
                    )
                )
                .Edit(fun _ -> dispatch (Update.Edit (key, state.Value.Draft)))
                .Doc()

    module TodoList =

        let Render dispatch (state: View<Model.TodoList>) =
            MasterTemplate()
                .TODOs(V(state.V.Todos).DocSeqCached(Model.TodoEntry.Key, TodoEntry.Render dispatch))
                .ClearCompleted(fun _ -> dispatch Update.ClearCompleted)
                .IsCompleted(
                    Var.Make
                        (V(
                            let todos = state.V.Todos
                            not (List.isEmpty todos)
                            && todos |> List.forall (fun t -> t.IsCompleted)
                        ))
                        (fun s -> dispatch (Update.SetAllCompleted s))
                )
                .Edit(fun e ->
                    if e.Event.Key = "Enter" then
                        dispatch (Update.New !e.Vars.Task)
                        e.Vars.Task := ""
                        e.Event.PreventDefault()
                )
                .CssFilterAll(Attr.ClassPred "selected" (Route.location.V = Route.EndPoint.All))
                .CssFilterActive(Attr.ClassPred "selected" (Route.location.V = Route.EndPoint.Active))
                .CssFilterCompleted(Attr.ClassPred "selected" (Route.location.V = Route.EndPoint.Completed))
                .Bind()

[<SPAEntryPoint>]
let Main () =
    let initState : Model.TodoList = { Todos = [] }
    MVU.Run initState Update.Dispatch Render.TodoList.Render
