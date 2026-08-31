using Dispatcher.DependencyInjection.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Registration;

public sealed class PipelineBehaviorRegistrationTests
{
    [Fact]
    public void Rejects_invalid_pipeline_behavior_type()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddPipelineBehavior(typeof(string)));
    }

    [Fact]
    public void Rejects_abstract_pipeline_behavior_type()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddPipelineBehavior(typeof(AbstractGreetingBehavior)));

        Assert.Equal("behaviorType", exception.ParamName);
    }

    [Fact]
    public void Rejects_partially_closed_open_pipeline_behavior_type()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddPipelineBehavior(typeof(PartiallyClosedGreetingBehavior<>)));
    }

    [Fact]
    public void Rejects_open_pipeline_behavior_with_fixed_response_type()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddPipelineBehavior(typeof(FixedResponseBehavior<>)));
    }

    [Fact]
    public void Rejects_reordered_open_pipeline_behavior_type_parameters()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddPipelineBehavior(typeof(ReorderedBehavior<,>)));
    }

    [Fact]
    public void Registers_canonical_open_pipeline_behavior_type()
    {
        var services = new ServiceCollection();

        services.AddPipelineBehavior(typeof(OpenPassthroughBehavior<,>));

        var descriptor = Assert.Single(services);
        Assert.Equal(typeof(IPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(typeof(OpenPassthroughBehavior<,>), descriptor.ImplementationType);
    }

    [Fact]
    public void Canonical_open_pipeline_behavior_can_limit_requests_with_constraints()
    {
        var services = new ServiceCollection();
        services.AddPipelineBehavior(typeof(TransactionalBehavior<,>));
        using var provider = TestServices.BuildProvider(services);
        using var scope = provider.CreateScope();

        Assert.Empty(scope.ServiceProvider.GetServices<IPipelineBehavior<GreetingQuery, string>>());
        Assert.Single(scope.ServiceProvider.GetServices<IPipelineBehavior<TransactionalQuery, string>>());
    }
}