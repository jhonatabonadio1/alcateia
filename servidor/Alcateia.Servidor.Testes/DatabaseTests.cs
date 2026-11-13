using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alcateia.Servidor.Banco;

namespace Alcateia.Servidor.Testes;

/// <summary>O cliente de PostgreSQL da casa, contra um Postgres de verdade.</summary>
public static class DatabaseTests
{
	public static readonly (string, Func<Task>)[] All =
	{
		("banco: entra com senha (SCRAM), e com senha errada não entra", async () =>
		{
			await using (PgConnection ok = await PgConnection.Open(World.Database()))
				Check.Equal("1", (await ok.Query("select 1 as um")).Text(0, "um"), "select 1");

			PgOptions wrong = World.Database();
			wrong.Password = "errada";
			await Check.Throws<PgException>(async () => await PgConnection.Open(wrong), "senha errada");
		}),

		("banco: parâmetros vão à parte do comando — aspas, acentos e nulo voltam iguais", async () =>
		{
			await using PgConnection c = await PgConnection.Open(World.Database());
			string nasty = "Robert'); drop table accounts; -- ção \"aspas\" \\ 🐺";
			PgResult r = await c.Query("select $1::text as texto, $2::int + 1 as numero, $3::boolean as sim, $4::text as nada, $5::bigint as grande",
				nasty, 41, true, null, 9007199254740993L);

			Check.Equal(nasty, r.Text(0, "texto"), "texto com aspas e acento");
			Check.Equal(42, r.Int(0, "numero"), "inteiro");
			Check.True(r.Bool(0, "sim"), "booleano");
			Check.True(r.Text(0, "nada") == null, "nulo volta nulo");
			Check.Equal(9007199254740993L, r.Long(0, "grande"), "inteiro grande");
		}),

		("banco: erro vem com o código, e a conexão continua servindo", async () =>
		{
			await using PgConnection c = await PgConnection.Open(World.Database());
			await c.Script("create temp table t (k int primary key)");
			await c.Query("insert into t values ($1)", 1);

			try
			{
				await c.Query("insert into t values ($1)", 1);
				throw new Failed("chave repetida devia falhar");
			}
			catch (PgException e)
			{
				Check.True(e.IsUniqueViolation, $"código de chave repetida (veio {e.Code})");
			}

			Check.Equal(1, (await c.Query("select count(*) as n from t")).Int(0, "n"), "a conexão segue boa depois do erro");
			Check.Equal(3, (await c.Query("insert into t select generate_series(10, 12)")).Affected, "linhas mexidas");
		}),

		("banco: transação que falha não deixa rastro", async () =>
		{
			await using var pool = new PgPool(World.Database());
			await pool.Use(async c => { await c.Script("drop table if exists tx; create table tx (k int)"); return 0; });

			await Check.Throws<InvalidOperationException>(() => pool.Transaction<int>(async c =>
			{
				await c.Query("insert into tx values (1)");
				throw new InvalidOperationException("no meio");
			}), "a exceção sai da transação");
			Check.Equal(0, (await pool.Query("select count(*) as n from tx")).Int(0, "n"), "nada ficou");

			await pool.Transaction(async c => { await c.Query("insert into tx values (2)"); return 0; });
			Check.Equal(1, (await pool.Query("select count(*) as n from tx")).Int(0, "n"), "a que deu certo ficou");
		}),

		("banco: cem consultas ao mesmo tempo com oito conexões", async () =>
		{
			PgOptions o = World.Database();
			o.MaxConnections = 8;
			await using var pool = new PgPool(o);

			IEnumerable<Task<int>> many = Enumerable.Range(0, 100).Select(async i =>
				(await pool.Query("select $1::int * 2 as dobro, pg_sleep(0.005)", i)).Int(0, "dobro"));
			int[] results = await Task.WhenAll(many);
			Check.True(results.Select((v, i) => v == i * 2).All(x => x), "cada consulta recebe a resposta dela");
		}),

		("banco: as migrações rodam uma vez só", async () =>
		{
			await using var pool = new PgPool(World.Database());
			await pool.Use(async c => { await c.Script("drop schema public cascade; create schema public;"); return 0; });

			Check.Equal(0, await Schema.Migrate(pool), "primeira vez: estava na versão 0");
			Check.Equal(Schema.Latest, await Schema.Migrate(pool), "segunda vez: já estava na última");
			Check.Equal(0, (await pool.Query("select count(*) as n from accounts")).Int(0, "n"), "a tabela de contas existe");
		}),
	};
}
