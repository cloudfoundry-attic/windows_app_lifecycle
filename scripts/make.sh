#!/bin/sh
set -e
set -x

echo "Experimental cross platform build with mono and go cross-compile"

rm -rf output
rm -rf packages

GOPATH=$PWD/diego-release  GOOS=windows GOARCH=amd64  go build -o diego-sshd.exe github.com/cloudfoundry-incubator/diego-ssh/cmd/sshd

nuget restore 
xbuild /p:TargetFrameworkVersion="v4.5"

bsdtar -czvf windows_app_lifecycle.tgz --exclude log -C Builder/bin . -C ../../Launcher/bin . -C ../../Healthcheck/bin . -C ../.. ./diego-sshd.exe
