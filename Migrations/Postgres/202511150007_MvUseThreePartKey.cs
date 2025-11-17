using Ae.Rail.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ae.Rail.Migrations.Postgres
{
	[DbContext(typeof(PostgresDbContext))]
	[Migration("202511150007_MvUseThreePartKey")]
	public partial class _202511150007_MvUseThreePartKey : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(@"drop view if exists trainservice_current;");
			migrationBuilder.Sql(@"drop materialized view if exists trainservice_v1 cascade;");

			migrationBuilder.Sql(@"
				create materialized view trainservice_v1 as
				select distinct on (""OperationalTrainNumber"", ""ServiceDate"", ""OriginStd"")
				  (payload->'OperationalTrainNumberIdentifier'->>'OperationalTrainNumber') as ""OperationalTrainNumber"",
				  to_char(
					(payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,StartDate}')::timestamp
					::date,
					'YYYY-MM-DD'
				  ) as ""ServiceDate"",
				  to_char((payload#>>'{Allocation,0,TrainOriginDateTime}')::timestamp, 'HH24:MI') as ""OriginStd"",
				  to_char((payload#>>'{Allocation,0,TrainDestDateTime}')::timestamp, 'HH24:MI') as ""Sta"",
				  (payload#>>'{Allocation,0,TrainOriginLocation,LocationPrimaryCode}') as ""OriginLocationPrimaryCode"",
				  (payload#>>'{Allocation,0,TrainDestLocation,LocationPrimaryCode}') as ""DestLocationPrimaryCode"",
				  (payload#>>'{Allocation,0,TrainOriginLocation,LocationSubsidiaryIdentification,LocationSubsidiaryCode}') as ""OriginLocationName"",
				  (payload#>>'{Allocation,0,TrainDestLocation,LocationSubsidiaryIdentification,LocationSubsidiaryCode}') as ""DestLocationName"",
				  (payload#>>'{Allocation,0,TrainOriginDateTime}')::timestamp as ""TrainOriginDateTime"",
				  (payload#>>'{Allocation,0,TrainDestDateTime}')::timestamp as ""TrainDestDateTime"",
				  (payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,Core}') as ""ToiCore"",
				  (payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,Variant}') as ""ToiVariant"",
				  nullif((payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,TimetableYear}'),'')::int as ""ToiTimetableYear"",
				  (payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,StartDate}')::date as ""ToiStartDate"",
				  now() as ""LastUpdatedAt""
				from message_envelopes
				where payload ? 'OperationalTrainNumberIdentifier'
				  and (payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,StartDate}') is not null
				order by ""OperationalTrainNumber"", ""ServiceDate"", ""OriginStd"", received_at desc;
			");

			migrationBuilder.Sql(@"create unique index if not exists ux_trainservice_v1_key on trainservice_v1 (""OperationalTrainNumber"",""ServiceDate"",""OriginStd"");");
			migrationBuilder.Sql(@"create index if not exists ix_trainservice_v1_servicedate on trainservice_v1 (""ServiceDate"");");
			migrationBuilder.Sql(@"create index if not exists ix_trainservice_v1_origin_dest on trainservice_v1 (""OriginLocationPrimaryCode"", ""DestLocationPrimaryCode"");");
			migrationBuilder.Sql(@"create index if not exists ix_trainservice_v1_origin_dest_name on trainservice_v1 (""OriginLocationName"", ""DestLocationName"");");
			migrationBuilder.Sql(@"create or replace view trainservice_current as select * from trainservice_v1;");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(@"drop view if exists trainservice_current;");
			migrationBuilder.Sql(@"drop materialized view if exists trainservice_v1 cascade;");

			// Revert to 4-part key version
			migrationBuilder.Sql(@"
				create materialized view trainservice_v1 as
				select distinct on (""OperationalTrainNumber"", ""ServiceDate"", ""OriginStd"", ""TrainOriginDateTime"")
				  (payload->'OperationalTrainNumberIdentifier'->>'OperationalTrainNumber') as ""OperationalTrainNumber"",
				  to_char(
					(payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,StartDate}')::timestamp
					::date,
					'YYYY-MM-DD'
				  ) as ""ServiceDate"",
				  to_char((payload#>>'{Allocation,0,TrainOriginDateTime}')::timestamp, 'HH24:MI') as ""OriginStd"",
				  to_char((payload#>>'{Allocation,0,TrainDestDateTime}')::timestamp, 'HH24:MI') as ""Sta"",
				  (payload#>>'{Allocation,0,TrainOriginLocation,LocationPrimaryCode}') as ""OriginLocationPrimaryCode"",
				  (payload#>>'{Allocation,0,TrainDestLocation,LocationPrimaryCode}') as ""DestLocationPrimaryCode"",
				  (payload#>>'{Allocation,0,TrainOriginLocation,LocationSubsidiaryIdentification,LocationSubsidiaryCode}') as ""OriginLocationName"",
				  (payload#>>'{Allocation,0,TrainDestLocation,LocationSubsidiaryIdentification,LocationSubsidiaryCode}') as ""DestLocationName"",
				  (payload#>>'{Allocation,0,TrainOriginDateTime}')::timestamp as ""TrainOriginDateTime"",
				  (payload#>>'{Allocation,0,TrainDestDateTime}')::timestamp as ""TrainDestDateTime"",
				  (payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,Core}') as ""ToiCore"",
				  (payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,Variant}') as ""ToiVariant"",
				  nullif((payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,TimetableYear}'),'')::int as ""ToiTimetableYear"",
				  (payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,StartDate}')::date as ""ToiStartDate"",
				  now() as ""LastUpdatedAt""
				from message_envelopes
				where payload ? 'OperationalTrainNumberIdentifier'
				  and (payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,StartDate}') is not null
				order by ""OperationalTrainNumber"", ""ServiceDate"", ""OriginStd"", ""TrainOriginDateTime"", received_at desc;
			");

			migrationBuilder.Sql(@"
				create unique index if not exists ux_trainservice_v1_key
				  on trainservice_v1 (""OperationalTrainNumber"", ""ServiceDate"", ""OriginStd"", ""TrainOriginDateTime"");
			");
			migrationBuilder.Sql(@"create index if not exists ix_trainservice_v1_servicedate on trainservice_v1 (""ServiceDate"");");
			migrationBuilder.Sql(@"create index if not exists ix_trainservice_v1_origin_dest on trainservice_v1 (""OriginLocationPrimaryCode"", ""DestLocationPrimaryCode"");");
			migrationBuilder.Sql(@"create index if not exists ix_trainservice_v1_origin_dest_name on trainservice_v1 (""OriginLocationName"", ""DestLocationName"");");
			migrationBuilder.Sql(@"create or replace view trainservice_current as select * from trainservice_v1;");
		}
	}
}

