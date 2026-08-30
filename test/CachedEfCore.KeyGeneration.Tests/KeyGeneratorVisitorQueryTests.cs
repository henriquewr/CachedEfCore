using CachedEfCore.Configuration;
using CachedEfCore.Context;
using CachedEfCore.DependencyInjection;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
               services.AddDbContext<TestDbContext>(options =>
               {
                    options.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UseSqlServer();

                        cachedEfCoreOptions.ConfigureKeyGeneration(keyGen =>
                        {
                            keyGen.ConfigureNonEvaluableTypes(configuration =>
                            {
                                return nonEvaluableTypes.ToList();
                            });
                        });
                    });
               });
           });
        
        public static TheoryData<Func<TestDbContext, Expression>, Type[]> GetNonEvaluableQueryTestCases()
        {
            return new()
            {
                {
                    context => context.LazyLoadEntity.Where(x => ThrowMethod(x.Id)).Select(x => x.LazyLoadPropId).Expression,
                    CachedEfCoreKeyGenerationOptions.DefaultNonEvaluableTypes.ToArray()
                },
                {
                    context => context.LazyLoadEntity.Where(x => ThrowMethod(x.Id)).Expression,
                    CachedEfCoreKeyGenerationOptions.DefaultNonEvaluableTypes.ToArray()
                },
                {
                    context => context.LazyLoadEntity.Select(x => x.StringData!.Where(s => ThrowMethod(x.Id) && ThrowMethod(x.Id))).Expression,
                    CachedEfCoreKeyGenerationOptions.DefaultNonEvaluableTypes.ToArray()
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
        public void KeyGeneratorVisitor_Should_Not_Eval_Query(Func<TestDbContext, Expression> getExpression, Type[] nonEvaluableTypes)
        {
            var serviceProvider = CreateProvider(nonEvaluableTypes);

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            var expression = getExpression(dbContext);

            ThrowMethodCalled = false;

            var keyGeneratorVisitor = dbContext.GetService<KeyGeneratorVisitor>();

            var result = keyGeneratorVisitor.ExpressionToString(expression);

            Assert.False(ThrowMethodCalled);
        }


        public static TheoryData<Func<TestDbContext, Expression>, Type[]> GetEfFunctionsTestCases()
        {
            return new()
            {
                {
                    context => context.LazyLoadEntity.Where(x => TestDbContext.CustomDbFunctionPlus(1, 2) < 1).Expression,
                    CachedEfCoreKeyGenerationOptions.DefaultNonEvaluableTypes.ToArray()
                },
                {
                    context => context.LazyLoadEntity.Where(x => EF.Functions.Random() < 1).Expression,
                    CachedEfCoreKeyGenerationOptions.DefaultNonEvaluableTypes.ToArray()
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetEfFunctionsTestCases))]
        public void KeyGeneratorVisitor_Should_Not_Eval_EF_Functions_Query(Func<TestDbContext, Expression> getExpression, Type[] nonEvaluableTypes)
        {
            // Expected to throw
            var serviceProvider = CreateProvider(nonEvaluableTypes);

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            var expression = getExpression(dbContext);

            var keyGeneratorVisitor = dbContext.GetService<KeyGeneratorVisitor>();

            var result = keyGeneratorVisitor.ExpressionToString(expression);
        }

        public class TestDbContext : CachedDbContext
        {
            public static int CustomDbFunctionPlus(int value, int value2)
                => throw new NotSupportedException("Custom database function should not be evaluated");

            public TestDbContext(DbContextOptions options) : base(options)
            {
            }

            public TestDbContext() : base()
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
                optionsBuilder.UseSqlServer();
                base.OnConfiguring(optionsBuilder);
            }

            public DbSet<LazyLoadEntity> LazyLoadEntity { get; set; }
            public DbSet<NonLazyLoadEntity> NonLazyLoadEntity { get; set; }
            public DbSet<AnotherLazyLoadEntity> AnotherLazyLoadEntity { get; set; }
        }

        public class AnotherLazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            [ForeignKey(nameof(LazyLoadProp))]
            public int? LazyLoadPropId { get; set; }

            [ForeignKey(nameof(LazyLoadPropId))]
            public virtual LazyLoadEntity? LazyLoadProp { get; set; }
        }

        public class LazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            [ForeignKey(nameof(LazyLoadProp))]
            public int? LazyLoadPropId { get; set; }

            [ForeignKey(nameof(LazyLoadPropId))]
            public virtual NonLazyLoadEntity? LazyLoadProp { get; set; }
        }

        public class NonLazyLoadEntity
        {
            [Key]
            public int Id { get; set; }

            public string? StringData { get; set; }
        }
    }
}
