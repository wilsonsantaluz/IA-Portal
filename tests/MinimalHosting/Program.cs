using AiGateway.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

void Check(bool value,string message){if(!value)throw new Exception(message);}
DefaultHttpContext Context(string path,string method="POST") {var c=new DefaultHttpContext();c.Request.Path=path;c.Request.Method=method;c.Response.Body=new MemoryStream();return c;}
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Hosting:Minimal","true"}}).Build();
var calls=0;var middleware=new MinimalHostingMiddleware(_=>{calls++;return Task.CompletedTask;},config);
foreach(var path in new[]{"/api/ocr/analisar","/api/sql/ask","/api/data/1/analytics","/api/data-connectors","/api/kb/reindex-all"}){
    var c=Context(path);await middleware.InvokeAsync(c);Check(c.Response.StatusCode==503,"Disabled feature gate "+path);
}
Check(calls==0,"Disabled requests cannot reach a service");
var sources=Context("/api/data/sources","GET");await middleware.InvokeAsync(sources);sources.Response.Body.Position=0;Check(await new StreamReader(sources.Response.Body).ReadToEndAsync()=="[]","Honest empty source list");
config["Hosting:Minimal"]="false";await middleware.InvokeAsync(Context("/api/ocr/analisar"));Check(calls==1,"Standard profile preserved");config["Hosting:Minimal"]="true";
var entered=0;var twoEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var busy=new MinimalHostingMiddleware(async _=>{if(Interlocked.Increment(ref entered)==2)twoEntered.TrySetResult();await release.Task;},config);
var first=busy.InvokeAsync(Context("/api/ia/ask/stream"));var second=busy.InvokeAsync(Context("/api/chatBot/ask"));await twoEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
var denied=Context("/api/ia/ask/stream");await busy.InvokeAsync(denied);Check(denied.Response.StatusCode==429&&entered==2,"At most two chat calls globally");release.SetResult();await Task.WhenAll(first,second);
var failing=new MinimalHostingMiddleware(_=>throw new InvalidOperationException("Synthetic downstream failure"),config);
for(var i=0;i<3;i++){try{await failing.InvokeAsync(Context("/api/ia/ask/stream"));}catch(InvalidOperationException){}}
var after=Context("/api/ia/ask/stream");await middleware.InvokeAsync(after);Check(after.Response.StatusCode==200&&calls==2,"Chat slot released after failures");
Console.WriteLine("PASS: minimal feature isolation/empty sources, unchanged standard profile, shared two-chat cap and release after downstream failure.");
