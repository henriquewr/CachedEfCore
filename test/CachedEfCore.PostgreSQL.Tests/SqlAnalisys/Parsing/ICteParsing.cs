namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing
{
    internal interface ICteParsing
    {
        void Cte(string sql);
        void Nested_Cte(string sql);
        void Cte_With_State_Changing_Query(string sql);
        void Cte_Multiple(string sql);
        void Cte_Recursive(string sql);
        void Cte_As_Not_Materialized(string sql);
        void Cte_As_Materialized(string sql);
        void Cte_Search(string sql);
        void Cte_Cycle(string sql);
    }
}
