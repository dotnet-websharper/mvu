namespace WebSharper.Mvu.Tests

open WebSharper
open WebSharper.JavaScript
open WebSharper.JQuery
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Html
open WebSharper.UI.Templating
open WebSharper.Mvu

module Remoting =

    [<Rpc>]
    let SendToServer (entries: Map<string, string>) =
        async {
            return Map.count entries
        }

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
            Entries : Map<string, string>
            Input : string
            ServerResponse : option<int>
        }

    type Message =
        | Goto of EndPoint
        | SetEntry of string * string
        | RemoveEntry of string
        | SendToServer
        | ServerReplied of int

    let Update (message: Message) (model: Model) =
        match message with
        | SetEntry (k, v) ->
            SetModel { model with Entries = Map.add k v model.Entries }
        | RemoveEntry k ->
            SetModel {
                model with
                    Entries = Map.remove k model.Entries
                    EndPoint =
                        match model.EndPoint with
                        | EditEntry k' when k = k' -> Home
                        | ep -> ep
            }
        | Goto ep ->
            SetModel { model with EndPoint = ep }
        | SendToServer ->
            DispatchAsync ServerReplied (Remoting.SendToServer model.Entries)
        | ServerReplied x ->
            SetModel { model with ServerResponse = Some x }

    module Pages =

        let showDate() =
            p [] [text ("Rendered at " + Date().ToTimeString())]

        let Home = Page.Single(attrs = [Attr.Class "home-page"], usesTransition = true, render = fun dispatch model ->
            let inp = Elt.input [Attr.Class "input"] []
            Doc.Concat [
                showDate()
                h2 [Attr.Class "subtitle hidden"] [text "Entries:"]
                div [Attr.Class "section"] [
                    div [Attr.Class "field has-addons"] [
                        div [Attr.Class "control"] [inp]
                        div [Attr.Class "control"] [
                            button [
                                Attr.Class "button"
                                on.click (fun _ _ -> dispatch (Goto (EndPoint.EditEntry inp.Value)))
                            ] [text "Add/edit entry"]
                        ]
                    ]
                ]
                table [Attr.Class "table is-fullwidth"] [
                    V(Map.toSeq model.V.Entries).DocSeqCached(fst, fun key (v: View<string * string>) ->
                        tr [] [
                            th [] [text key]
                            td [] [text (snd v.V)]
                            td [] [
                                div [Attr.Class "buttons has-addons"] [
                                    button [
                                        Attr.Class "button is-small"
                                        on.click (fun _ _ -> dispatch (Goto (EndPoint.EditEntry key)))
                                    ] [text "Edit"]
                                    button [
                                        Attr.Class "button is-small"
                                        on.click (fun _ _ -> dispatch (RemoveEntry key))
                                    ] [text "Remove"]
                                ]
                            ]
                        ])
                ]
                div [Attr.Class "field"] [
                    div [Attr.Class "control"] [
                        button [
                            Attr.Class "button"
                            on.click (fun _ _ -> dispatch SendToServer)
                        ] [text "Send to server"]
                    ]
                ]
                label [Attr.Class "label"] [
                    text (match model.V.ServerResponse with None -> "" | Some x -> string x)
                ]
            ])

        let EditEntry = Page.Create(attrs = [Attr.Class "entry-page"], usesTransition = true, render = fun key dispatch model ->
            let value = V(Map.tryFind key model.V.Entries |> Option.defaultValue "")
            let var = Var.Make value (fun v -> dispatch (SetEntry (key, v)))
            div [Attr.Class "section"] [
                showDate()
                label [Attr.Class "label"] [text ("Editing value for key: " + key)]
                div [Attr.Class "field has-addons"] [
                    div [Attr.Class "control"] [Doc.Input [Attr.Class "input"] var]
                    div [Attr.Class "control"] [
                        button [
                            Attr.Class "button"
                            on.click (fun _ _ -> dispatch (Goto EndPoint.Home))
                        ] [text "Ok"]
                    ]
                    button [
                        Attr.Class "button"
                        on.click (fun _ _ -> dispatch (RemoveEntry key))
                    ] [text "Remove"]
                ]
            ])

    let Render mdl =
        match mdl.EndPoint with
        | EndPoint.Home -> Pages.Home ()
        | EndPoint.EditEntry key -> Pages.EditEntry key

    let InitModel =
        {
            EndPoint = EndPoint.Home
            Entries = Map.empty
            Input = ""
            ServerResponse = None
        }

    [<SPAEntryPoint>]
    let Main () =
        App.CreatePaged InitModel Update Render
        |> App.WithLocalStorage "mvu-tests"
        |> App.Run
        |> Doc.RunPrepend JS.Document.Body
