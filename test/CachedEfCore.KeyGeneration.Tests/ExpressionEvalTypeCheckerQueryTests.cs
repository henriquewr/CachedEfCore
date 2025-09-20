using CachedEfCore.Cache;
using CachedEfCore.Context;
using CachedEfCore.DependencyInjection;
using CachedEfCore.Interceptors;
using CachedEfCore.KeyGeneration.EvalTypeChecker;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace CachedEfCore.KeyGeneration.Tests
{
    public class ExpressionEvalTypeCheckerQueryTests
    {
        public ExpressionEvalTypeCheckerQueryTests()
        {

        }

        private static ExpressionEvalTypeCheckerVisitor CreateEvalTypeChecker(params IEnumerable<Type> types)
        {
            return new ExpressionEvalTypeCheckerVisitor(new TypeCompatibilityChecker(types));
        }

        public static TheoryData<Expression, IExpressionEvalTypeChecker> GetNonEvalQueriesTestCases()
        {
            var dbContext = new TestDbContext(null!);

            var defaultTypeChecker = CreateEvalTypeChecker(CachedEfCoreOptions.DefaultNonEvaluableTypes);

            return new()
            {
                {
                    Expression.Constant(dbContext.AnotherLazyLoadEntity),
                    defaultTypeChecker
                },
                {
                    dbContext.AnotherLazyLoadEntity.Where(x => x.Id != 0).Expression,
                    defaultTypeChecker
                },
                {
                    dbContext.AnotherLazyLoadEntity.Where(x => x.Id != 0).Select(x => x.LazyLoadProp).Expression,
                    defaultTypeChecker
                },
            };
        }

        [Theory]
        [MemberData(nameof(GetNonEvalQueriesTestCases))]
        public void ExpressionEvalTypeChecker_Should_Return_True_For_Query(Expression expression, IExpressionEvalTypeChecker expressionEvalTypeChecker)
        {
            var willEval = expressionEvalTypeChecker.WillEvalTypes(expression);

            Assert.True(willEval);
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
