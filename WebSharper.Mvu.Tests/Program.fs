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

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open WebSharper.AspNetCore

module Program =

    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)
        
        //Add services to the container.
        builder.Services.AddWebSharper()
            .AddAuthentication("WebSharper")
            .AddCookie("WebSharper", fun options -> ())
        |> ignore

        let app = builder.Build()

        // Configure the HTTP request pipeline.
        if not (app.Environment.IsDevelopment()) then
            app.UseExceptionHandler("/Error")
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                .UseHsts()
            |> ignore

        app.UseHttpsRedirection()
            .UseAuthentication()
            .UseDefaultFiles()
            .UseStaticFiles()
            .UseWebSharper(fun builder ->
                builder.UseSitelets(false) |> ignore
            )
        |> ignore

        app.Run()

        0 // Exit code
