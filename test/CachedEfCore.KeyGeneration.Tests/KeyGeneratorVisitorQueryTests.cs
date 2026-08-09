using CachedEfCore.Cache;
using CachedEfCore.Configuration;
using CachedEfCore.Context;
using CachedEfCore.DependencyInjection;
using CachedEfCore.Interceptors;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Xunit;

namespace CachedEfCore.KeyGeneration.Tests
{
    public class KeyGeneratorVisitorQueryTests : IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        public KeyGeneratorVisitorQueryTests(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
        }

        protected virtual IServiceProvider CreateProvider(params IEnumerable<Type> nonEvaluableTypes)
           => _serviceProviderFixture.CreateProvider(services =>
           {
               services.AddDbContext<TestDbContext>();

               services.AddCachedEfCore<SqlServerQueryEntityExtractor>((sp, options) =>
               {
                   options.ConfigureNonEvaluableTypes(configuration =>
                   {
                       configuration.Clear();
                       configuration.AddRange(nonEvaluableTypes);
                   });
               });
           });

        private KeyGeneratorVisitor CreateVisitor(params IEnumerable<Type> nonEvaluableTypes)
        {
            return CreateProvider(nonEvaluableTypes).CreateScope().ServiceProvider.GetRequiredService<KeyGeneratorVisitor>();
        }
     
        public static TheoryData<Expression, Type[], IModel> GetNonEvaluableQueryTestCases()
        {
            var context = new TestDbContext(null!);

            return new()
            {
                {
                    context.LazyLoadEntity.Where(x => ThrowMethod(x.Id)).Select(x => x.LazyLoadPropId).Expression,
                    CachedEfCoreOptions.DefaultNonEvaluableTypes.ToArray(),
                    context.Model
                },
                {
                    context.LazyLoadEntity.Where(x => ThrowMethod(x.Id)).Expression,
                    CachedEfCoreOptions.DefaultNonEvaluableTypes.ToArray(),
                    context.Model
                },
                {
                    context.LazyLoadEntity.Select(x => x.StringData!.Where(s => ThrowMethod(x.Id) && ThrowMethod(x.Id))).Expression,
                    CachedEfCoreOptions.DefaultNonEvaluableTypes.ToArray(),
                    context.Model
                },
            };

        }

        [ThreadStatic]
        private static bool ThrowMethodCalled;
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ThrowMethod(int anything)
        {
            ThrowMethodCalled = true;
            throw new InvalidOperationException("Should not be called");
        }

        [Theory]
        [MemberData(nameof(GetNonEvaluableQueryTestCases))]
        public void KeyGeneratorVisitor_Should_Not_Eval_Query(Expression expression, Type[] nonEvaluableTypes, IModel model)
        {
            ThrowMethodCalled = false;

            var keyGeneratorVisitor = CreateVisitor(nonEvaluableTypes);

            var result = keyGeneratorVisitor.ExpressionToString(expression, model);

            Assert.False(ThrowMethodCalled);
        }


        public static TheoryData<Expression, Type[], IModel> GetEfFunctionsTestCases()
        {
            var context = new TestDbContext(null!);

            return new()
            {
                {
                    context.LazyLoadEntity.Where(x => TestDbContext.CustomDbFunctionPlus(1, 2) < 1).Expression,
                    CachedEfCoreOptions.DefaultNonEvaluableTypes.ToArray(),
                    context.Model
                },
                {
                    context.LazyLoadEntity.Where(x => EF.Functions.Random() < 1).Expression,
                    CachedEfCoreOptions.DefaultNonEvaluableTypes.ToArray(),
                    context.Model
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetEfFunctionsTestCases))]
        public void KeyGeneratorVisitor_Should_Not_Eval_EF_Functions_Query(Expression expression, Type[] nonEvaluableTypes, IModel model)
        {
            // Expected to throw
            var keyGeneratorVisitor = CreateVisitor(nonEvaluableTypes);
            keyGeneratorVisitor.ExpressionToString(expression, model);
        }

        private class TestDbContext : CachedDbContext
        {
            public static int CustomDbFunctionPlus(int value, int value2)
                => throw new NotSupportedException("Custom database function should not be evaluated");

            public TestDbContext(IDbQueryCacheStore dbQueryCacheStore) : base(dbQueryCacheStore)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.HasDbFunction(typeof(TestDbContext).GetMethod(nameof(CustomDbFunctionPlus), [typeof(int), typeof(int)])!)
                    .HasTranslation(
                        args =>
                            new SqlBinaryExpression(
                                ExpressionType.Add,
                                new SqlConstantExpression(args[0], new IntTypeMapping("int", DbType.Int32)),
                                new SqlConstantExpression(args[1], new IntTypeMapping("int", DbType.Int32)),
                                args[0].Type,
                                args[0].TypeMapping));

                base.OnModelCreating(modelBuilder);
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseNpgsql(Guid.NewGuid().ToString()).AddInterceptors(new DbStateInterceptor(new SqlServerQueryEntityExtractor()));
                base.OnConfiguring(optionsBuilder);
            }

            public DbSet<LazyLoadEntity> LazyLoadEntity { get; set; }
            public DbSet<NonLazyLoadEntity> NonLazyLoadEntity { get; set; }
            public DbSet<AnotherLazyLoadEntity> AnotherLazyLoadEntity { get; set; }
        }

        private class AnotherLazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            [ForeignKey(nameof(LazyLoadProp))]
            public int? LazyLoadPropId { get; set; }

            [ForeignKey(nameof(LazyLoadPropId))]
            public virtual LazyLoadEntity? LazyLoadProp { get; set; }
        }

        private class LazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            [ForeignKey(nameof(LazyLoadProp))]
            public int? LazyLoadPropId { get; set; }

            [ForeignKey(nameof(LazyLoadPropId))]
            public virtual NonLazyLoadEntity? LazyLoadProp { get; set; }
        }

        private class NonLazyLoadEntity
        {
            [Key]
            public int Id { get; set; }

            public string? StringData { get; set; }
        }
    }
}
