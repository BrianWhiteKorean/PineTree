$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$sourcePath = Join-Path $PSScriptRoot '../Classes/NetworkUtility.cs'
$testSource = @'
namespace PineTree.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using PineTree.Classes;

    public static class LoginNetworkRegression
    {
        private sealed class FakeHandler : HttpMessageHandler
        {
            public int Calls;
            public Func<int, HttpResponseMessage> Respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(Respond(++Calls));
            }
        }

        private static HttpResponseMessage Response(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }

        private static void Check(string name, Func<int, HttpResponseMessage> respond, string expected, int expectedCalls)
        {
            FieldInfo field = typeof(NetworkUtility).GetField("Client", BindingFlags.Static | BindingFlags.NonPublic);
            object original = field.GetValue(null);
            FakeHandler handler = new FakeHandler { Respond = respond };
            using (HttpClient client = new HttpClient(handler))
            {
                field.SetValue(null, client);
                try
                {
                    string actual = new NetworkUtility().GetPublicIPAddressAsync().GetAwaiter().GetResult();
                    if (actual != expected || handler.Calls != expectedCalls)
                        throw new Exception(name + ": " + actual + ", calls=" + handler.Calls);
                    Console.WriteLine("PASS: " + name);
                }
                finally { field.SetValue(null, original); }
            }
        }

        public static void Run()
        {
            Check("IPv4 whitespace", call => Response(" 203.0.113.7\r\n"), "203.0.113.7", 1);
            Check("IPv6", call => Response("2001:db8::7"), "2001:db8::7", 1);
            Check("Invalid response uses fallback", call => Response(call == 1 ? "<html>error</html>" : "203.0.113.8"), "203.0.113.8", 2);
            Check("Timeout uses fallback", call => { if (call == 1) throw new TaskCanceledException(); return Response("203.0.113.9"); }, "203.0.113.9", 2);
            Check("Network failure remains nonfatal", call => { throw new HttpRequestException("offline"); }, "0.0.0.0", 2);
            Check("HTTP failures remain nonfatal", call => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), "0.0.0.0", 2);
            Check("Invalid addresses remain nonfatal", call => Response(call == 1 ? "127.0.0.1" : "::"), "0.0.0.0", 2);
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                try
                {
                    new NetworkUtility().GetPublicIPAddressAsync(cancellation.Token).GetAwaiter().GetResult();
                    throw new Exception("Cancellation was ignored");
                }
                catch (OperationCanceledException) { Console.WriteLine("PASS: Cancellation"); }
            }
        }
    }
}
'@
$compilerPath = Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$temporaryBase = Join-Path ([IO.Path]::GetTempPath()) ('PineTreeLoginNetwork-' + [Guid]::NewGuid().ToString('N'))
$testPath = $temporaryBase + '.cs'
$assemblyPath = $temporaryBase + '.dll'
try {
    Set-Content -LiteralPath $testPath -Value $testSource -Encoding UTF8
    & $compilerPath /nologo /target:library "/out:$assemblyPath" /reference:System.Net.Http.dll $sourcePath $testPath
    if ($LASTEXITCODE -ne 0) { throw 'Network regression test compilation failed.' }
    [void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($assemblyPath))
} finally {
    Remove-Item -LiteralPath $testPath, $assemblyPath -ErrorAction SilentlyContinue
}
[PineTree.Tests.LoginNetworkRegression]::Run()
