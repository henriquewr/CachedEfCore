using CachedEfCore.Cache;
using CachedEfCore.Context;
using CachedEfCore.DependencyInjection;
using CachedEfCore.Interceptors;
using CachedEfCore.KeyGeneration.EvalTypeChecker;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Xunit;

namespace CachedEfCore.KeyGeneration.Tests
{
    public class KeyGeneratorVisitorQueryTests
    {
        private static readonly PrintabilityChecker DefaultPrintabilityChecker = new PrintabilityChecker();
        private static KeyGeneratorVisitor CreateVisitor(params IEnumerable<Type> nonEvaluableTypes)
        {
            return new KeyGeneratorVisitor
            (
                DefaultPrintabilityChecker,
                new ExpressionEvalTypeCheckerVisitor(new TypeCompatibilityChecker(nonEvaluableTypes)),
                CachedEfCoreOptions.DefaultKeyGeneratorJsonSerializerOptions
            );
        }

        public static TheoryData<Expression, KeyGeneratorVisitor> GetNonEvaluableQueryTestCases()
        {
            var context = new TestDbContext(null!);

            return new()
            {
                {
                    context.LazyLoadEntity.Where(x => ThrowMethod(x.Id)).Select(x => x.LazyLoadPropId).Expression,
                    CreateVisitor(CachedEfCoreOptions.DefaultNonEvaluableTypes)
                },
                {
                    context.LazyLoadEntity.Where(x => ThrowMethod(x.Id)).Expression,
                    CreateVisitor(CachedEfCoreOptions.DefaultNonEvaluableTypes)
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
        public void KeyGeneratorVisitor_Should_Not_Eval_Query(Expression expression, KeyGeneratorVisitor keyGeneratorVisitor)
        {
            ThrowMethodCalled = false;

            var result = keyGeneratorVisitor.ExpressionToString(expression);

            Assert.False(ThrowMethodCalled);
        }


        private class TestDbContext : CachedDbContext
        {
            public TestDbContext(IDbQueryCacheStore dbQueryCacheStore) : base(dbQueryCacheStore)
            {
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase("test").AddInterceptors(new DbStateInterceptor(new SqlServerQueryEntityExtractor()));
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
