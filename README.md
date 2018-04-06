# WebSharper.Mvu

This library implements an Elm-inspired MVU (Model-View-Update) architecture for WebSharper client-side applications.

It includes a sample [TodoMVC](https://todomvc.com) application which showcases the features of the library.

## The TodoMvc sample

The folder `WebSharper.Mvu.TodoMvc` contains an implementation of the popular [TodoMVC](https://todomvc.com/) application.

### Building and running the application

To build the application (and the library), simply run the following command:

```bash
dotnet build
```

To run the application, you have two options.

* With Kestrel (ASP.NET Core):

    ```bash
    dotnet run -p WebSharper.Mvu.TodoMvc
    ```

* With `webpack-dev-server`:

    ```bash
    npm install     # Run once to restore npm packages
    npm run dev     # Start the server
    ```
    
    Or:
    
    ```bash
    yarn install    # Run once to restore npm packages
    yarn run dev    # Start the server
    ```

The application supports time-travelling debugging using [RemoteDev](https://github.com/zalmoxisus/remotedev). Here is how to use it:

* Start the remotedev server:

    ```bash
    npm install         # Run once to restore npm packages
    npm run remotedev   # Start the remotedev server
    ```
    
    Or:
    
    ```bash
    yarn install        # Run once to restore npm packages
    yarn run remotedev  # Start the remotedev server
    ```

* Install and start the [Redux devtools extension](https://github.com/zalmoxisus/redux-devtools-extension#installation) for your browser.

### Code walkthrough

The whole application is contained in the file [TodoMvc.fs](WebSharper.Mvu.TodoMvc/TodoMvc.fs). The other F# files in this folder simply provide a server for running with `dotnet`.

The general structure of a WebSharper.Mvu application should be familiar if you know MVU architectures such as Elm or Redux.

* At the core of the architecture is the **model**, of type `Model.TodoList`. This is the entire state of the application, defined as an immutable record.

* The application logic is implemented as a **pure** function `Update.TodoList` which defines how the model is updated.

    The update function is called on every user input: text input, checkbox click, etc. This action is encoded as a **message**, of type `Update.Message`. The update function computes the new model based on the old model and the message.

* Finally, the application is **render**ed by the function `Render.TodoList`. This function takes care of binding the dynamic state to the DOM. It also binds event handlers to send messages using `dispatch`.

* All these elements are connected together in the entry point by `App.Create`.

* Additionally, the **route** is defined in the `Route` module and allows the application to react to changes to the URL.
