[<WebSharper.JavaScript>]
[<WebSharper.Require(typeof<WebSharper.Mvu.Resources.RemoteDevJs>)>]
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

[<Inline "RemoteDev.connectViaExtension($options)">]
let ConnectViaExtension(options: Options) = X<Connection>

[<Inline "RemoteDev.extractState($s)">]
let ExtractState(s: obj) = X<obj>
