namespace WebSharper.Mvu.Tests

open WebSharper
open WebSharper.JavaScript
open WebSharper.JQuery
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Html
open WebSharper.UI.Templating
open WebSharper.Mvu

[<JavaScript>]
module Client =
    // The templates are loaded from the DOM, so you just can edit index.html
    // and refresh your browser, no need to recompile unless you add or remove holes.
    type IndexTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>

    type EndPoint =
        | Home
        | EditEntry of string

    type Model =
        {
            EndPoint : EndPoint
            Counter : int
            Entries : Map<string, string>
            Input : string
        }

    type Message =
        | Goto of EndPoint
        | Increment
        | Decrement
        | SetEntry of string * string
        | RemoveEntry of string

    let Update (message: Message) (model: Model) =
        match message with
        | Increment ->
            { model with Counter = model.Counter + 1 }
        | Decrement ->
            { model with Counter = model.Counter - 1 }
        | SetEntry (k, v) ->
            { model with Entries = Map.add k v model.Entries }
        | RemoveEntry k ->
            { model with
                Entries = Map.remove k model.Entries
                EndPoint =
                    match model.EndPoint with
                    | EditEntry k' when k = k' -> Home
                    | ep -> ep
            }
        | Goto ep ->
            { model with EndPoint = ep }

    module Pages =

        let showDate() =
            p [] [text ("Rendered at " + Date().ToTimeString())]

        let Home = Page.Single(fun dispatch model ->
            let inp = input [] []
            Doc.Concat [
                showDate()
                button [on.click (fun _ _ -> dispatch Decrement)] [text "-"]
                text (string model.V.Counter)
                button [on.click (fun _ _ -> dispatch Increment)] [text "+"]
                h2 [] [text "Entries:"]
                p [] [
                    inp
                    button [on.click (fun _ _ -> dispatch (Goto (EndPoint.EditEntry inp.Value)))] [text "Add/edit entry"]
                ]
                table [] [
                    V(Map.toSeq model.V.Entries).DocSeqCached(fst, fun key (v: View<string * string>) ->
                        tr [] [
                            th [] [text key]
                            td [] [text (snd v.V)]
                            td [] [
                                button [on.click (fun _ _ -> dispatch (Goto (EndPoint.EditEntry key)))] [text "Edit"]
                                button [on.click (fun _ _ -> dispatch (RemoveEntry key))] [text "Remove"]
                            ]
                        ])
                ]
            ])

        let EditEntry = Page.Create(fun key dispatch model ->
            let value = V(Map.tryFind key model.V.Entries |> Option.defaultValue "")
            let var = Var.Make value (fun v -> dispatch (SetEntry (key, v)))
            Doc.Concat [
                showDate()
                p [] [text ("Editing value for key: " + key)]
                Doc.Input [] var
                button [on.click (fun _ _ -> dispatch (Goto EndPoint.Home))] [text "Ok"]
                button [on.click (fun _ _ -> dispatch (RemoveEntry key))] [text "Remove"]
            ])

    let Render mdl =
        match mdl.EndPoint with
        | EndPoint.Home -> Pages.Home ()
        | EndPoint.EditEntry key -> Pages.EditEntry key

    let InitModel =
        {
            EndPoint = EndPoint.Home
            Counter = 0
            Entries = Map.empty
            Input = ""
        }

    [<SPAEntryPoint>]
    let Main () =
        App.CreatePaged InitModel Update Render
        |> App.WithLocalStorage "mvu-tests"
        |> App.Run
        |> Doc.RunPrepend JS.Document.Body
