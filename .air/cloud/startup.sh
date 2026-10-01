#!/bin/sh
set -e

# Install tools only if they are missing
if ! command -v dotnet >/dev/null; then
apt-get update
apt-get install -y dotnet-sdk-10.0
fi