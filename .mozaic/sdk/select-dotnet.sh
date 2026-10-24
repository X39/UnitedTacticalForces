#!/bin/sh
# Fail rather than silently resolve a different SDK through an ancestor global.json.
set -eu
case "$(basename "$0")" in
  utf-dotnet-201) dotnet=/usr/share/dotnet/dotnet; expected=10.0.201 ;;
  utf-dotnet-302) dotnet=/opt/utf-dotnet-302/dotnet; expected=10.0.302 ;;
  *) exit 2 ;;
esac
test "$("$dotnet" --version)" = "$expected" || {
  echo 'The current directory selects a different SDK. Use the reviewed isolated build context.' >&2
  exit 1
}
exec "$dotnet" "$@"
