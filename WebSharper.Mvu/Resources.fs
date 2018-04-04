namespace WebSharper.Mvu.Resources

open WebSharper

type RemoteDevJs() = inherit Resources.BaseResource("remotedev.js")

[<WebResource("remotedev.js", "application/javascript")>]
do()
