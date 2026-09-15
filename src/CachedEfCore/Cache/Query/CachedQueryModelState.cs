using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Threading;

namespace CachedEfCore.Cache.Query
{
    internal sealed class CachedQueryModelState<TResult>
    {
        private readonly object _initializationLock = new();

        private IModel? _model;
        private bool _isDbContextDependent;

        public IModel GetModel(DbContext dbContext, out DbContextId? dependentDbContextId)
        {
            var model = dbContext.Model;
            var initializedModel = Volatile.Read(ref _model);

            if (!ReferenceEquals(initializedModel, model))
            {
                initializedModel = Initialize(dbContext, model);
            }

            dependentDbContextId = _isDbContextDependent ? dbContext.ContextId : null;

            return initializedModel;
        }

        private IModel Initialize(DbContext dbContext, IModel model)
        {
            lock (_initializationLock)
            {
                if (_model is null)
                {
                    var dependencyManager = dbContext.GetService<EntityDependency>();

                    _isDbContextDependent = dependencyManager.HasLazyLoad(typeof(TResult));
                    Volatile.Write(ref _model, model);

                    return model;
                }

                if (!ReferenceEquals(_model, model))
                {
                    throw new InvalidOperationException("A compiled cached query can only be used with a single Entity Framework model.");
                }

                return _model;
            }
        }
    }
}
