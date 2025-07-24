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
module WebSharper.ReduxDevTools

open WebSharper
open WebSharper.JavaScript
    
[<Stub>]
type Options [<Inline "{}">] () =
    [<DefaultValue>] val mutable name : string
    [<DefaultValue>] val mutable actionCreators : obj
    [<DefaultValue>] val mutable latency : int
    [<DefaultValue>] val mutable maxAge : int
    [<DefaultValue>] val mutable trace : obj
    [<DefaultValue>] val mutable traceLimit : int
    [<DefaultValue>] val mutable serialize : obj
    [<DefaultValue>] val mutable actionsDenylist : obj
    [<DefaultValue>] val mutable actionsAllowlist : obj
    [<DefaultValue>] val mutable predicate : System.Func<obj, obj, bool>
    [<DefaultValue>] val mutable shouldRecordChanges : bool
    [<DefaultValue>] val mutable pauseActionType : bool
    [<DefaultValue>] val mutable autoPause : bool
    [<DefaultValue>] val mutable shouldStartLocked : bool
    [<DefaultValue>] val mutable shouldHotReload : bool
    [<DefaultValue>] val mutable shouldCatchErrors : bool
    [<DefaultValue>] val mutable features : obj
    [<DefaultValue>] val mutable actionSanitizer : System.Func<obj, int, obj>
    [<DefaultValue>] val mutable stateSanitizer : System.Func<obj, int, obj>

[<Inline "$global.__REDUX_DEVTOOLS_EXTENSION__.connect($0)">]
let Connect(options: Options) = X<RemoteDev.Connection>
