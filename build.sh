#!/bin/bash

set -e

paket() {
    if [ "$OS" = "Windows_NT" ]; then
        .paket/paket.exe "$@"
    else
        mono .paket/paket.exe "$@"
    fi
}

if [ "$WsUpdate" == "" ]; then
    paket restore
else
    paket update -g wsbuild --no-restore
fi

exec paket-files/wsbuild/github.com/dotnet-websharper/build-script/WebSharper.Fake.sh "$@"
