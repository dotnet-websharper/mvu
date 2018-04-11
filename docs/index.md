# MVU

MVU is a library which implements a convenient architecture to write client-side applications in F#. It is designed to be:

* **Predictable.** The application's state, or **model**, is stored in a single location and all changes to it go through a single **update** function.

* **Debuggable.** This unique storage makes it easy to verify the full state of the application and its history. In particular, support for [RemoteDev](https://github.com/zalmoxisus/remotedev) is built in.

* **Performant.** MVU relies on the proven [WebSharper.UI](https://github.com/dotnet-websharper/ui) reactive library to render model changes to the DOM.

* **Maintainable.** The architecture provides a great separation of concerns between application logic and display, which makes it easy to maintain. The display can be loaded directly from HTML templates, so that developers and designers can easily collaborate.


# The architecture

At the core of the architecture is the **model**. This is an immutable type that you define (generally a record type) which contains all of the application's state.
