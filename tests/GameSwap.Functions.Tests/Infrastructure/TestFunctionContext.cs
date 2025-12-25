using System.Collections.ObjectModel;
using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GameSwap.Functions.Tests.Infrastructure;

internal sealed class TestFunctionContext : FunctionContext
{
    private readonly Dictionary<object, object> _items = new();
    private readonly IServiceProvider _services = new ServiceCollection().BuildServiceProvider();
    private readonly TestInvocationFeatures _features = new();
    private readonly TestFunctionDefinition _definition = new();

    public override string InvocationId { get; } = Guid.NewGuid().ToString();
    public override string FunctionId { get; } = Guid.NewGuid().ToString();
    public override TraceContext TraceContext { get; } = new TestTraceContext();
    public override BindingContext BindingContext { get; } = new TestBindingContext();
    public override RetryContext? RetryContext { get; } = null;
    public override IServiceProvider InstanceServices { get => _services; set { } }
    public override IDictionary<object, object> Items => _items;
    public override IInvocationFeatures Features => _features;
    public override FunctionDefinition FunctionDefinition => _definition;
}

internal sealed class TestHttpRequestData : HttpRequestData
{
    public TestHttpRequestData(FunctionContext functionContext) : base(functionContext)
    {
        Headers = new HttpHeadersCollection();
        Body = new MemoryStream();
        Url = new Uri("http://localhost");
    }

    public override Stream Body { get; set; }
    public override HttpHeadersCollection Headers { get; }
    public override IReadOnlyCollection<IHttpCookie> Cookies => Array.Empty<IHttpCookie>();
    public override Uri Url { get; }
    public override IEnumerable<ClaimsIdentity> Identities => Array.Empty<ClaimsIdentity>();
    public override string Method { get; set; } = "GET";

    public override HttpResponseData CreateResponse()
        => new TestHttpResponseData(FunctionContext);
}

internal sealed class TestHttpResponseData : HttpResponseData
{
    public TestHttpResponseData(FunctionContext functionContext) : base(functionContext)
    {
        Headers = new HttpHeadersCollection();
        Body = new MemoryStream();
        Cookies = new HttpCookies();
    }

    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; }
    public override Stream Body { get; set; }
    public override HttpCookies Cookies { get; }
}

internal sealed class TestBindingContext : BindingContext
{
    public override IReadOnlyDictionary<string, object> BindingData { get; } = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
}

internal sealed class TestTraceContext : TraceContext
{
    public override string TraceParent { get; set; } = string.Empty;
    public override string TraceState { get; set; } = string.Empty;
    public override IDictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
}

internal sealed class TestFunctionDefinition : FunctionDefinition
{
    public override string PathToAssembly => string.Empty;
    public override string EntryPoint => string.Empty;
    public override string Id => Guid.NewGuid().ToString();
    public override string Name => "TestFunction";
    public override IReadOnlyDictionary<string, BindingMetadata> InputBindings { get; } = new ReadOnlyDictionary<string, BindingMetadata>(new Dictionary<string, BindingMetadata>());
    public override IReadOnlyDictionary<string, BindingMetadata> OutputBindings { get; } = new ReadOnlyDictionary<string, BindingMetadata>(new Dictionary<string, BindingMetadata>());
    public override IEnumerable<FunctionParameter> Parameters { get; } = Array.Empty<FunctionParameter>();
}

internal sealed class TestInvocationFeatures : IInvocationFeatures
{
    private readonly Dictionary<Type, object> _features = new();

    public TFeature? Get<TFeature>() where TFeature : class
    {
        return _features.TryGetValue(typeof(TFeature), out var feature) ? feature as TFeature : null;
    }

    public void Set<TFeature>(TFeature? instance) where TFeature : class
    {
        if (instance is null)
        {
            _features.Remove(typeof(TFeature));
            return;
        }

        _features[typeof(TFeature)] = instance;
    }
}
