using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing.SelectParsing
{
    public class SelectPostgreSqlParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly PostgreSqlParser _postgreSqlParser;

        public SelectPostgreSqlParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Theory]
        [InlineData("""SELECT * FROM Test;""")]
        [InlineData("""SELECT Id FROM Test;""")]
        [InlineData("""SELECT * FROM public.Test;""")]
        [InlineData("""SELECT * FROM public.Test AS t;""")]
        [InlineData("""SELECT * FROM Test t;""")]
        [InlineData("""SELECT * FROM ONLY Test;""")]
        [InlineData("""SELECT * FROM ONLY public.Test;""")]
        public void Select_From(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT DISTINCT Name FROM Test;""")]
        [InlineData("""SELECT DISTINCT Id, Name FROM Test;""")]
        [InlineData("""SELECT DISTINCT * FROM Test;""")]
        [InlineData("""SELECT DISTINCT ON (Id) * FROM Test ORDER BY Id;""")]
        [InlineData("""SELECT DISTINCT ON (Name) Id, Name FROM Test ORDER BY Name, Id;""")]
        [InlineData("""SELECT DISTINCT ON (Id, Name) * FROM Test ORDER BY Id, Name;""")]
        public void Select_Distinct(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT ALL Name FROM Test;""")]
        [InlineData("""SELECT ALL Id, Name FROM Test;""")]
        [InlineData("""SELECT ALL * FROM Test;""")]
        [InlineData("""SELECT ALL Name FROM Test WHERE Id > 0;""")]
        public void Select_All(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT * FROM Test INNER JOIN Test2 ON Test.Id = Test2.Id;""")]
        [InlineData("""SELECT * FROM Test JOIN Test2 ON Test.Id = Test2.Id;""")]
        [InlineData("""SELECT * FROM Test LEFT JOIN Test2 ON Test.Id = Test2.Id;""")]
        [InlineData("""SELECT * FROM Test LEFT OUTER JOIN Test2 ON Test.Id = Test2.Id;""")]
        [InlineData("""SELECT * FROM Test RIGHT JOIN Test2 ON Test.Id = Test2.Id;""")]
        [InlineData("""SELECT * FROM Test RIGHT OUTER JOIN Test2 ON Test.Id = Test2.Id;""")]
        [InlineData("""SELECT * FROM Test FULL JOIN Test2 ON Test.Id = Test2.Id;""")]
        [InlineData("""SELECT * FROM Test FULL OUTER JOIN Test2 ON Test.Id = Test2.Id;""")]
        [InlineData("""SELECT * FROM Test CROSS JOIN Test2;""")]
        [InlineData("""SELECT * FROM Test JOIN Test2 USING (Id);""")]
        [InlineData("""SELECT * FROM Test NATURAL JOIN Test2;""")]
        [InlineData("""SELECT * FROM Test NATURAL LEFT JOIN Test2;""")]
        [InlineData("""SELECT * FROM Test NATURAL RIGHT JOIN Test2;""")]
        [InlineData("""SELECT * FROM Test NATURAL FULL JOIN Test2;""")]
        public void Select_Join(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            SELECT * FROM Test
        )
        SELECT * FROM Cte;
        """)]
                [InlineData("""
        WITH Cte AS MATERIALIZED (
            SELECT * FROM Test
        )
        SELECT * FROM Cte;
        """)]
        [InlineData("""
        WITH Cte AS NOT MATERIALIZED (
            SELECT * FROM Test
        )
        SELECT * FROM Cte;
        """)]
        [InlineData("""
        WITH Cte AS (
            SELECT * FROM Test
        ),
        Cte2 AS (
            SELECT * FROM Test2
        )
        SELECT * FROM Cte JOIN Cte2 ON Cte.Id = Cte2.Id;
        """)]
        public void Select_Cte(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""
        WITH RECURSIVE Cte AS (
            SELECT Id, ParentId FROM Test WHERE ParentId IS NULL
            UNION ALL
            SELECT t.Id, t.ParentId
            FROM Test t
            JOIN Cte c ON t.ParentId = c.Id
        )
        SELECT * FROM Cte;
        """)]
        [InlineData("""
        WITH RECURSIVE Cte AS (
            SELECT 1 AS Id
            UNION ALL
            SELECT Id + 1 FROM Cte WHERE Id < 10
        )
        SELECT * FROM Cte;
        """)]
        public void Select_Recursive(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT * FROM Test WHERE Id = 1;""")]
        [InlineData("""SELECT * FROM Test WHERE Id > 1;""")]
        [InlineData("""SELECT * FROM Test WHERE Id IN (1, 2, 3);""")]
        [InlineData("""SELECT * FROM Test WHERE Id NOT IN (1, 2, 3);""")]
        [InlineData("""SELECT * FROM Test WHERE Id BETWEEN 1 AND 10;""")]
        [InlineData("""SELECT * FROM Test WHERE Name LIKE 'A%';""")]
        [InlineData("""SELECT * FROM Test WHERE Name ILIKE 'a%';""")]
        [InlineData("""SELECT * FROM Test WHERE Name IS NULL;""")]
        [InlineData("""SELECT * FROM Test WHERE Name IS NOT NULL;""")]
        [InlineData("""SELECT * FROM Test WHERE EXISTS (SELECT 1 FROM Test2);""")]
        [InlineData("""SELECT * FROM Test WHERE Id IN (SELECT Id FROM Test2);""")]
        public void Select_Where(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT Name FROM Test GROUP BY Name;""")]
        [InlineData("""SELECT Name, COUNT(*) FROM Test GROUP BY Name;""")]
        [InlineData("""SELECT Name, COUNT(*) FROM Test GROUP BY Name HAVING COUNT(*) > 1;""")]
        [InlineData("""SELECT Name, COUNT(*) FROM Test GROUP BY ROLLUP (Name);""")]
        [InlineData("""SELECT Name, COUNT(*) FROM Test GROUP BY CUBE (Name);""")]
        [InlineData("""SELECT Id, Name FROM Test GROUP BY GROUPING SETS ((Id), (Name));""")]
        public void Select_GroupBy(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT * FROM Test ORDER BY Id;""")]
        [InlineData("""SELECT * FROM Test ORDER BY Id ASC;""")]
        [InlineData("""SELECT * FROM Test ORDER BY Id DESC;""")]
        [InlineData("""SELECT * FROM Test ORDER BY Id, Name;""")]
        [InlineData("""SELECT * FROM Test ORDER BY Name NULLS FIRST;""")]
        [InlineData("""SELECT * FROM Test ORDER BY Name NULLS LAST;""")]
        [InlineData("""SELECT * FROM Test ORDER BY 1;""")]
        public void Select_OrderBy(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT * FROM Test LIMIT 10;""")]
        [InlineData("""SELECT * FROM Test LIMIT ALL;""")]
        [InlineData("""SELECT * FROM Test OFFSET 10;""")]
        [InlineData("""SELECT * FROM Test LIMIT 10 OFFSET 20;""")]
        [InlineData("""SELECT * FROM Test OFFSET 10 ROWS;""")]
        [InlineData("""SELECT * FROM Test FETCH FIRST 10 ROWS ONLY;""")]
        [InlineData("""SELECT * FROM Test FETCH NEXT 10 ROWS ONLY;""")]
        [InlineData("""SELECT * FROM Test FETCH FIRST ROW ONLY;""")]
        [InlineData("""SELECT * FROM Test FETCH FIRST 10 ROWS WITH TIES;""")]
        public void Select_LimitOffsetFetch(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT Id FROM Test UNION SELECT Id FROM Test2;""")]
        [InlineData("""SELECT Id FROM Test UNION ALL SELECT Id FROM Test2;""")]
        [InlineData("""SELECT Id FROM Test INTERSECT SELECT Id FROM Test2;""")]
        [InlineData("""SELECT Id FROM Test INTERSECT ALL SELECT Id FROM Test2;""")]
        [InlineData("""SELECT Id FROM Test EXCEPT SELECT Id FROM Test2;""")]
        [InlineData("""SELECT Id FROM Test EXCEPT ALL SELECT Id FROM Test2;""")]
        public void Select_SetOperation(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT * FROM Test FOR UPDATE;""")]
        [InlineData("""SELECT * FROM Test FOR NO KEY UPDATE;""")]
        [InlineData("""SELECT * FROM Test FOR SHARE;""")]
        [InlineData("""SELECT * FROM Test FOR KEY SHARE;""")]
        [InlineData("""SELECT * FROM Test FOR UPDATE NOWAIT;""")]
        [InlineData("""SELECT * FROM Test FOR UPDATE SKIP LOCKED;""")]
        public void Select_LockingClause(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT * FROM Test TABLESAMPLE SYSTEM (10);""")]
        [InlineData("""SELECT * FROM Test TABLESAMPLE BERNOULLI (10);""")]
        [InlineData("""SELECT * FROM Test TABLESAMPLE SYSTEM (10) REPEATABLE (42);""")]
        [InlineData("""SELECT * FROM Test TABLESAMPLE BERNOULLI (10) REPEATABLE (42);""")]
        public void Select_TableSample(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""VALUES (1), (2), (3);""")]
        [InlineData("""VALUES (1, 'A'), (2, 'B');""")]
        [InlineData("""SELECT * FROM (VALUES (1, 'A'), (2, 'B')) AS t(Id, Name);""")]
        public void Select_Values(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""SELECT * FROM LATERAL (SELECT * FROM Test) AS t;""")]
        [InlineData("""SELECT * FROM Test t CROSS JOIN LATERAL (SELECT t.Id) AS x;""")]
        [InlineData("""SELECT * FROM generate_series(1, 10) AS n;""")]
        [InlineData("""SELECT * FROM unnest(ARRAY[1, 2, 3]) AS n;""")]
        [InlineData("""SELECT * FROM generate_series(1, 10) WITH ORDINALITY AS n;""")]
        public void Select_LateralAndTableFunctions(string sql)
        {
            AssertEmpty(sql);
        }

        [Theory]
        [InlineData("""TABLE Test;""")]
        public void Select_Table(string sql)
        {
            AssertEmpty(sql);
        }

        private void AssertEmpty(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Empty(identifiers);
        }
    }
}
