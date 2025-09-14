using Casbin;

/// <summary>
/// A static set of extensions to make it nicer to use the enforcer.
/// </summary>
public static class EnforcerExtensions
{
    public static PolicyBuilder ForSubject<TEntity>(
        this IEnforcer enforcer,
        TEntity entity,
        string domain
    )
        where TEntity : IEntity
    {
        return ForSubject(enforcer, entity.Id.ToString(), domain);
    }

    public static PolicyBuilder ForSubject(
        this IEnforcer enforcer,
        string entityId,
        string domain
    )
    {
        return new PolicyBuilder(enforcer, entityId, domain);
    }
}

public record PolicyBuilder(IEnforcer enforcer, string subject, string domain)
{
    private string _subject = subject;
    private string _domain = domain;

    public PolicyBuilder Grant<TActionEnum>(TActionEnum action, string resource)
        where TActionEnum : struct, Enum
    {
        return this.Grant((action, resource));
    }

    public PolicyBuilder Grant<TActionEnum, TTarget>(
        TActionEnum action,
        TTarget target
    )
        where TActionEnum : struct, Enum
        where TTarget : IEntity
    {
        return this.Grant((action, target.Id.ToString()));
    }

    public PolicyBuilder Grant<TActionEnum>(
        params (TActionEnum Action, string Resource)[] policies
    )
        where TActionEnum : struct, Enum
    {
        foreach (var (action, resource) in policies)
        {
            enforcer.AddPolicy(_subject, resource, action.ToString(), _domain);
        }

        return this;
    }

    public async Task SaveAsync()
    {
        await enforcer.SavePolicyAsync();
    }

    public async Task<bool> VerifyAsync<TEnumAction, TEntity>(
        TEnumAction action,
        TEntity resource
    )
        where TEnumAction : struct, Enum
        where TEntity : IEntity
    {
        return await VerifyAsync(action, resource.Id.ToString());
    }

    public Task<bool> VerifyAsync<TEnumAction>(TEnumAction action, string resource)
        where TEnumAction : struct, Enum =>
        enforcer.EnforceAsync(_subject, resource, action.ToString(), _domain);
}
