using CachedEfCore.KeyGeneration.EvalTypeChecker;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;

namespace CachedEfCore.KeyGeneration.Tests
{
    public class TypeCompatibilityCheckerTests
    {
        public static TheoryData<Type, bool, TypeCompatibilityChecker> GetTypeCompatibilityCheckerNonGenericTestCases()
        {
            return new()
            {
                {
                    typeof(BaseClass),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseClass),
                        }
                    )
                },
                {
                    typeof(BaseClass),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseAbstractClass),
                        }
                    )
                },
                {
                    typeof(BaseClass),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IBaseInterface),
                        }
                    )
                },
                {
                    typeof(BaseClass),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(ISomeInterface),
                        }
                    )
                },
                {
                    typeof(BaseClass),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DerivatedClass),
                        }
                    )
                },
                {
                    typeof(BaseClass),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(EvenMoreDerivatedClass),
                        }
                    )
                },


                {
                    typeof(EvenMoreDerivatedClass),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseClass),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedClass),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseAbstractClass),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedClass),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IBaseInterface),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedClass),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(ISomeInterface),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedClass),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DerivatedClass),
                        }
                    )
                },
                {
                    typeof(DerivatedClass),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(EvenMoreDerivatedClass),
                        }
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetTypeCompatibilityCheckerNonGenericTestCases))]
        public void TypeCompatibilityChecker_Should_Check_IsCompatible_NonGeneric(Type typeToCheck, bool expectedResult, TypeCompatibilityChecker typeCompatibilityChecker)
        {
            var isCompatible = typeCompatibilityChecker.IsCompatible(typeToCheck);

            Assert.Equal(expectedResult, isCompatible);
        }


        public static TheoryData<Type, bool, TypeCompatibilityChecker> GetTypeCompatibilityCheckerOpenGenericTestCases()
        {
            return new()
            {
                {
                    typeof(BaseGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseGenericClass<>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IBaseGenericInterface<>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(ISomeGenericInterface<>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DerivatedGenericClass<>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(EvenMoreDerivatedGenericClass<>),
                        }
                    )
                },


                {
                    typeof(EvenMoreDerivatedGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseGenericClass<>),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IBaseGenericInterface<>),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(ISomeGenericInterface<>),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DerivatedGenericClass<>),
                        }
                    )
                },
                {
                    typeof(DerivatedGenericClass<>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(EvenMoreDerivatedGenericClass<>),
                        }
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetTypeCompatibilityCheckerOpenGenericTestCases))]
        public void TypeCompatibilityChecker_Should_Check_IsCompatible_OpenGeneric(Type typeToCheck, bool expectedResult, TypeCompatibilityChecker typeCompatibilityChecker)
        {
            var isCompatible = typeCompatibilityChecker.IsCompatible(typeToCheck);

            Assert.Equal(expectedResult, isCompatible);
        }


        public static TheoryData<Type, bool, TypeCompatibilityChecker> GetTypeCompatibilityCheckerClosedGenericTestCases()
        {
            return new()
            {
                {
                    typeof(BaseGenericClass<int>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseGenericClass<int>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<int>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IBaseGenericInterface<int>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<int>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DerivatedGenericClass<int>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<int>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(ISomeGenericInterface<int>),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<int>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(ISomeGenericInterface<int>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<int>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(EvenMoreDerivatedGenericClass<int>),
                        }
                    )
                },


                {
                    typeof(EvenMoreDerivatedGenericClass<int>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseGenericClass<int>),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<int>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DerivatedGenericClass<int>),
                        }
                    )
                },
                {
                    typeof(DerivatedGenericClass<int>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(EvenMoreDerivatedGenericClass<int>),
                        }
                    )
                },




                {
                    typeof(BaseGenericClass<decimal>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseGenericClass<int>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<decimal>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DerivatedGenericClass<int>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<decimal>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IBaseGenericInterface<int>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<decimal>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(EvenMoreDerivatedGenericClass<int>),
                        }
                    )
                },


                {
                    typeof(EvenMoreDerivatedGenericClass<decimal>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseGenericClass<int>),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<decimal>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DerivatedGenericClass<int>),
                        }
                    )
                },
                {
                    typeof(DerivatedGenericClass<decimal>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(EvenMoreDerivatedGenericClass<int>),
                        }
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetTypeCompatibilityCheckerClosedGenericTestCases))]
        public void TypeCompatibilityChecker_Should_Check_IsCompatible_ClosedGeneric(Type typeToCheck, bool expectedResult, TypeCompatibilityChecker typeCompatibilityChecker)
        {
            var isCompatible = typeCompatibilityChecker.IsCompatible(typeToCheck);

            Assert.Equal(expectedResult, isCompatible);
        }


        public static TheoryData<Type, bool, TypeCompatibilityChecker> GetTypeCompatibilityCheckerGenericTestCases()
        {
            var testDbContext = new TestDbContext();
            return new()
            {
                {
                    testDbContext.SomeEntity.Where(x => true).GetType(),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
#pragma warning disable EF1001
                            typeof(EntityQueryable<>),
#pragma warning restore EF1001
                        }
                    )
                },
                {
                    typeof(DbSet<SomeEntity>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DbSet<>),
                        }
                    )
                },
                {
                    testDbContext.SomeEntity.GetType(),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DbSet<>),
                        }
                    )
                },

                {
                    typeof(BaseGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseClass),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IBaseInterface),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(ISomeInterface),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<int>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IBaseGenericInterface<>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<int>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(ISomeGenericInterface<>),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(DerivatedClass),
                        }
                    )
                },
                {
                    typeof(BaseGenericClass<>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(EvenMoreDerivatedClass),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseClass),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<int>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseClass),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<int>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseAbstractClass),
                        }
                    )
                },
                {
                    typeof(EvenMoreDerivatedGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(BaseAbstractClass),
                        }
                    )
                },
                {
                    typeof(SomeGenericClass<>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(SomeGenericAbstractClass<>),
                        }
                    )
                },
                {
                    typeof(SomeGenericClass<int>),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(SomeGenericAbstractClass<>),
                        }
                    )
                },
                {
                    typeof(SomeGenericClass<int>),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(SomeGenericAbstractClass<bool>),
                        }
                    )
                },
                {
                    new List<int>().AsQueryable().GetType(),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IQueryable<>),
                        }
                    )
                },
                {
                    new List<int>().AsQueryable().GetType(),
                    true,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IEnumerable<>),
                        }
                    )
                },
                {
                    new List<int>().AsQueryable().GetType(),
                    false,
                    new TypeCompatibilityChecker(
                        new Type[]
                        {
                            typeof(IQueryable<decimal>),
                        }
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetTypeCompatibilityCheckerGenericTestCases))]
        public void TypeCompatibilityChecker_Should_Check_IsCompatible_Generic(Type typeToCheck, bool expectedResult, TypeCompatibilityChecker typeCompatibilityChecker)
        {
            var isCompatible = typeCompatibilityChecker.IsCompatible(typeToCheck);

            Assert.Equal(expectedResult, isCompatible);
        }



        private abstract class BaseAbstractClass;
        private abstract class SomeGenericAbstractClass<T>;



        private interface IBaseInterface;
        private interface IBaseGenericInterface<T>;
        private interface ISomeInterface;
        private interface ISomeGenericInterface<T>;



        private class BaseClass : BaseAbstractClass, IBaseInterface;
        private class DerivatedClass : BaseClass, ISomeInterface;
        private class EvenMoreDerivatedClass : DerivatedClass;



        private class BaseGenericClass<T> : BaseClass, IBaseGenericInterface<T>;
        private class DerivatedGenericClass<T> : BaseGenericClass<T>, ISomeGenericInterface<T>;
        private class EvenMoreDerivatedGenericClass<T> : DerivatedGenericClass<T>;
        private class SomeGenericClass<T> : SomeGenericAbstractClass<T>;



        private class TestDbContext : DbContext
        {
            public TestDbContext() : base()
            {
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase("test");
                base.OnConfiguring(optionsBuilder);
            }

            public DbSet<SomeEntity> SomeEntity { get; set; }
        }

        private class SomeEntity
        {
            [Key]
            public int Id { get; set; }

            public string? SomeData { get; set; }
        }
    }
}