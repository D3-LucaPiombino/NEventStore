namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Threading.Tasks;
	using FluentAssertions;
	using NEventStore.Persistence.AcceptanceTests;
	using NEventStore.Persistence.AcceptanceTests.BDD;
	using NEventStore.Persistence.Sql.SqlDialects;
	using NEventStore.Serialization;
	using Xunit;

    public class when_creating_sql_persistence_factory_with_oracle_native_dialect : SpecificationBase
    {
        private Exception _exception;

        protected override Task Because()
        {
            _exception = Catch.Exception(() => new SqlPersistenceFactory("Connection",
                new BinarySerializer(),
                new OracleNativeDialect()).Build());
			return Task.FromResult(true);
        }

        [Fact]
        public void should_not_throw()
        {
           _exception.Should().BeNull();
        }
    }
}