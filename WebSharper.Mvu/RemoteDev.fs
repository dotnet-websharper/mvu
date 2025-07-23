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
[<WebSharper.JavaScript>]
module WebSharper.RemoteDev

open WebSharper
open WebSharper.JavaScript

module MsgTypes =
    [<Literal>]
    let Start = "START"
    [<Literal>]
    let Action = "ACTION"
    [<Literal>]
    let Dispatch = "DISPATCH"

module PayloadTypes =
    [<Literal>]
    let ImportState = "IMPORT_STATE"
    [<Literal>]
    let JumpToState = "JUMP_TO_STATE"
    [<Literal>]
    let JumpToAction = "JUMP_TO_ACTION"
    [<Literal>]
    let Reset = "RESET"
    [<Literal>]
    let Rollback = "ROLLBACK"
    [<Literal>]
    let Commit = "COMMIT"

[<Stub>]
type Options [<Inline "{}">] () =
    [<DefaultValue>] val mutable remote : bool
    [<DefaultValue>] val mutable port : int
    [<DefaultValue>] val mutable hostname : string
    [<DefaultValue>] val mutable secure : bool
    [<DefaultValue>] val mutable getActionType : (obj -> obj)
    [<DefaultValue>] val mutable serialize : obj
    
type Action = 
    { 
        ``type``: string
        fields : obj array
    }

type LiftedState =
    {
        actionsById : Action array
        computedStates : obj array
        currentStateIndex : int
        nextActionId : int
    }
    
type Payload =
    {
        nextLiftedState : LiftedState
        ``type``: string
    }

type Msg =
    {
        state : string
        action : obj
        ``type`` : string
        payload : Payload
    }

type Listener = Msg -> unit
    
type Unsubscribe = unit -> unit

[<Stub>]
type Connection =
    member this.init(x: obj) = X<unit>
    member this.subscribe(l: Listener) = X<Unsubscribe>
    member this.unsubscribe() = X<unit>
    member this.send(x: obj, y: obj) = X<unit>
    member this.error(x: obj) = X<unit>

[<Import("connect", "remotedev")>]
let Connect(options: Options) = X<Connection>

[<Import("parse","jsan")>]
let parse (x: string) = X<obj>

[<Inline>]
let ExtractState(message: Msg) = parse message.state
