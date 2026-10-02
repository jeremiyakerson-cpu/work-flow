#!/usr/bin/env bash
# Offline audit: the game must never touch the network, track the user or
# embed ad/analytics SDKs. Fails (exit 1) if any source under Assets/ uses a
# networking/tracking API, or if Packages/manifest.json pulls in a networking,
# analytics, ads or online-services package.
#
#   Tools/offline-audit.sh            audit the TowerDefense project
#   Tools/offline-audit.sh <root>     audit another project root (used by --self-test)
#   Tools/offline-audit.sh --self-test   prove the audit catches violations
#
# Matches inside whole-line comments (//, ///, /* and * continuation lines) are
# ignored so documentation may name the forbidden APIs. POSIX ERE only (GNU and BSD/macOS grep).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"

if [[ "${1:-}" == "--self-test" ]]; then
  tmp="$(mktemp -d)"
  trap 'rm -rf "$tmp"' EXIT
  mkdir -p "$tmp/Assets/Scripts" "$tmp/Assets/Plugins/iOS" "$tmp/Packages"
  echo '{ "dependencies": { "com.unity.ugui": "2.0.0" } }' > "$tmp/Packages/manifest.json"
  printf '// UnityWebRequest is mentioned in a comment only\nclass Ok {}\n' > "$tmp/Assets/Scripts/Ok.cs"
  if ! "$0" "$tmp" > /dev/null; then echo "self-test: clean project was rejected" >&2; exit 1; fi

  fail_case() { # $1 file, $2 content
    rm -rf "$tmp/Assets/Scripts/Bad"* "$tmp/Assets/Plugins/iOS/Bad"*
    printf '%s\n' "$2" > "$tmp/$1"
    if "$0" "$tmp" > /dev/null 2>&1; then echo "self-test: missed violation in $1: $2" >&2; exit 1; fi
  }
  fail_case Assets/Scripts/Bad.cs 'var r = UnityEngine.Networking.UnityWebRequest.Get("x");'
  fail_case Assets/Scripts/Bad.cs 'using System.Net.Http;'
  fail_case Assets/Scripts/Bad.cs 'var c = new HttpClient();'
  fail_case Assets/Scripts/Bad.cs 'var s = new TcpClient("h", 1);'
  fail_case Assets/Scripts/Bad.cs 'var w = new WWW(url);'
  fail_case Assets/Scripts/Bad.cs 'Application.OpenURL("https://example.com");'
  fail_case Assets/Plugins/iOS/Bad.mm '[[NSURLSession sharedSession] dataTaskWithURL:u];'
  fail_case Assets/Plugins/iOS/Bad.mm '#import <AppTrackingTransparency/AppTrackingTransparency.h>'
  rm -rf "$tmp/Assets/Plugins/iOS/Bad"*
  echo '{ "dependencies": { "com.unity.modules.unitywebrequest": "1.0.0" } }' > "$tmp/Packages/manifest.json"
  if "$0" "$tmp" > /dev/null 2>&1; then echo "self-test: missed forbidden package" >&2; exit 1; fi
  echo "offline-audit self-test: all violations detected"
  exit 0
fi

root="${1:-$here/..}"
root="$(cd "$root" && pwd)"
assets="$root/Assets"
status=0

# C#/Unity: HTTP, sockets, DNS, web views, ads/analytics, leaving the app.
cs_pattern='UnityWebRequest|UnityEngine\.Networking|(^|[^[:alnum:]_])WWW([^[:alnum:]_]|$)|WWWForm|System\.Net([^[:alnum:]_]|$)|HttpClient|HttpWebRequest|WebRequest([^[:alnum:]_]|$)|WebClient|(^|[^[:alnum:]_])Socket([^[:alnum:]_]|$)|TcpClient|TcpListener|UdpClient|WebSocket|(^|[^[:alnum:]_])Dns\.|new Ping\(|UnityEngine\.Analytics|Analytics\.CustomEvent|UnityEngine\.Advertisements|Application\.OpenURL|AdvertisingIdentifier|RequestAdvertisingIdentifierAsync'
# Native iOS: URL loading, sockets, Network.framework, web views, tracking.
native_pattern='NSURLSession|NSURLConnection|NSURLRequest|NSMutableURLRequest|CFNetwork|CFSocket|CFStream|nw_connection|Network/Network\.h|<sys/socket\.h>|getaddrinfo|WKWebView|UIWebView|SFSafariViewController|ASIdentifierManager|AdSupport|ATTrackingManager|AppTrackingTransparency|openURL'

scan() { # $1 pattern, $2.. include globs
  local pattern="$1"; shift
  local includes=()
  for g in "$@"; do includes+=("--include=$g"); done
  # grep exits 1 when nothing matches; that is the good case.
  { grep -rnE "${includes[@]}" "$pattern" "$assets" 2>/dev/null || true; } |
    grep -vE '^[^:]+:[0-9]+:[[:space:]]*(//|/\*|\*)' || true
}

if [[ -d "$assets" ]]; then
  hits="$(scan "$cs_pattern" '*.cs' '*.jslib' '*.js')"
  if [[ -n "$hits" ]]; then
    echo "offline-audit: networking/tracking API in C# sources:" >&2
    echo "$hits" >&2
    status=1
  fi
  hits="$(scan "$native_pattern" '*.mm' '*.m' '*.h' '*.c' '*.cpp' '*.swift')"
  if [[ -n "$hits" ]]; then
    echo "offline-audit: networking/tracking API in native plugins:" >&2
    echo "$hits" >&2
    status=1
  fi
else
  echo "offline-audit: no Assets/ folder under $root" >&2
  status=1
fi

manifest="$root/Packages/manifest.json"
pkg_pattern='"com\.unity\.(modules\.unitywebrequest[a-z]*|modules\.unityanalytics|analytics|ads|ads\.ios-support|purchasing|services\.[a-z.]+|netcode[a-z.-]*|transport|multiplayer[a-z.-]*|remote-config|cloud[a-z.-]*|collab-proxy)"'
if [[ -f "$manifest" ]]; then
  hits="$(grep -nE "$pkg_pattern" "$manifest" || true)"
  if [[ -n "$hits" ]]; then
    echo "offline-audit: online/analytics/ads package in Packages/manifest.json:" >&2
    echo "$hits" >&2
    status=1
  fi
fi

if [[ $status -eq 0 ]]; then
  echo "offline-audit: no networking, tracking or online packages found"
fi
exit $status
