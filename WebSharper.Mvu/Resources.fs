namespace WebSharper.Mvu.Resources

open WebSharper

type RemoteDevJs() = inherit Resources.BaseResource("remotedev.js")
type PagerCss() = inherit Resources.BaseResource("page.css")

[<WebResource("remotedev.js", "application/javascript")>]
[<WebResource("page.css", "text/css")>]
do()
