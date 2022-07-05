#!/bin/bash -e

# NOTE: Meant for internal use, so basically no error checking of the environment
# It only needs 1 argument, a LiteCore build SHA

script_path=$(dirname "$(readlink -f "$BASH_SOURCE")")
pushd $script_path

if [ ! -d data ]; then
    git clone --filter=blob:none --sparse https://github.com/couchbase/query data
    pushd data
    git sparse-checkout init --cone
    git sparse-checkout add test/filestore/test_cases
else 
    pushd data
    git pull origin master
fi

popd

pushd N1QLQueryHarness
rm -f results.json
dotnet run -- prepare $1 -w $script_path
dotnet run -- migrate -w $script_path
dotnet run -- run -o -j results.json -w $script_path