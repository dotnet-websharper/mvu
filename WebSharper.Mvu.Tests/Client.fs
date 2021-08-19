// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2018 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}
namespace WebSharper.Mvu.Tests

open WebSharper
open WebSharper.JavaScript
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

    type EndPoint =
        | Home
        | EditEntry of string

    type Model =
        {
            EndPoint : EndPoint
            Entries : Map<string, string>
            Input : string
            ServerResponse : option<int>
            Counter : int
        }

    [<NamedUnionCases "type">]
    type Message =
        | Goto of endpoint: EndPoint
        | SetEntry of key: string * value: string
        | RemoveEntry of key: string
        | SendToServer
        | ServerReplied of response: int
        | AddTwoToCounter

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
        | AddTwoToCounter ->
            UpdateModel (fun m -> { m with Counter = m.Counter + 1 })
            +
            UpdateModel (fun m -> { m with Counter = m.Counter + 1 })

    module Pages =

        let showDate() =
            p [] [text ("Rendered at " + Date().ToTimeString())]

        let counter dispatch (model: View<Model>) =
            p [] [
                text (string model.V.Counter + " ")
                button [on.click (fun _ _ -> dispatch AddTwoToCounter)] [text "Increment by two"]
                text " (this tests that UpdateModel works correctly)"
            ]

        let Home = Page.Single(attrs = [Attr.Class "home-page"], usesTransition = true, render = fun dispatch model ->
            let inp = Elt.input [Attr.Class "input"] []
            let kinp = Elt.input [Attr.Class "input"] []
            let vinp = Elt.input [Attr.Class "input"] []
            Doc.Concat [
                showDate()
                counter dispatch model
                h2 [Attr.Class "subtitle hidden"] [text "Entries:"]
                div [Attr.Class "section"] [
                    div [Attr.Class "field has-addons"] [
                        div [Attr.Class "control"] [inp]
                        div [Attr.Class "control"] [
                            button [
                                Attr.Class "button"
                                on.click (fun _ _ -> dispatch (Goto (EndPoint.EditEntry inp.Value)))
                            ] [text "Go to add/edit entry page"]
                        ]
                    ]
                ]
                div [Attr.Class "section"] [
                    div [Attr.Class "field has-addons"] [
                        div [Attr.Class "control"] [kinp]
                        div [Attr.Class "control"] [vinp]
                        div [Attr.Class "control"] [
                            button [
                                Attr.Class "button"
                                on.click (fun _ _ -> dispatch (SetEntry(kinp.Value, vinp.Value)))
                            ] [text "Directly add/edit entry"]
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
            Counter = 0
        }

    [<SPAEntryPoint>]
    let Main () =
        App.CreatePaged InitModel Update Render
        |> App.WithLocalStorage "mvu-tests"
        |> App.WithLog (fun msg model ->
            New ["msg" => Json.Encode msg; "model" => Json.Encode model]
            |> Console.Log)
        |> App.Run
        |> Doc.RunPrepend JS.Document.Body
