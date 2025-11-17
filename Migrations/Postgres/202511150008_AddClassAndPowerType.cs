using Ae.Rail.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ae.Rail.Migrations.Postgres
{
	[DbContext(typeof(PostgresDbContext))]
	[Migration("202511150008_AddClassAndPowerType")]
	public partial class _202511150008_AddClassAndPowerType : Migration
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
				  lc.rail_classes as ""RailClasses"",
				  case
					when (lc.first_cls)::int between 1 and 70 then 'Diesel'
					when (lc.first_cls)::int between 71 and 96 then 'Electric'
					when (lc.first_cls)::int = 97 then 'Diesel'
					when (lc.first_cls)::int = 98 then 'Steam'
					when (lc.first_cls)::int between 101 and 299 then 'Diesel'
					when (lc.first_cls)::int between 300 and 398 then 'Electric'
					when (lc.first_cls)::int = 399 then 'Diesel'
					when (lc.first_cls)::int between 400 and 799 then 'Electric'
					when (lc.first_cls)::int = 800 then 'Diesel/Electric (bi-mode)'
					when (lc.first_cls)::int = 801 then 'Electric'
					when (lc.first_cls)::int = 802 then 'Diesel/Electric (bi-mode)'
					when (lc.first_cls)::int = 901 then 'Diesel'
					when (lc.first_cls)::int between 910 and 939 then 'Electric'
					when (lc.first_cls)::int between 950 and 999 then 'Diesel'
					else 'Unknown'
				  end as ""PowerType"",
				  now() as ""LastUpdatedAt""
				from message_envelopes
				left join lateral (
					select
						string_agg(c.cls, ',' order by c.src, c.cls) as rail_classes,
						(array_agg(c.cls order by c.src, c.cls))[1] as first_cls
					from (
						select distinct src, cls from (
							-- Units (U): first 3 chars of ResourceGroupId
							select 1 as src, substring((a->'ResourceGroup'->>'ResourceGroupId') from 1 for 3) as cls
							from jsonb_array_elements(payload->'Allocation') as a
							where (a->'ResourceGroup'->>'TypeOfResource') ilike 'U' and length(a->'ResourceGroup'->>'ResourceGroupId') >= 3
							union
							-- Locos (L): first 2 chars of each VehicleId
							select 2 as src, substring((veh->>'VehicleId') from 1 for 2) as cls
							from jsonb_array_elements(payload->'Allocation') as a
							left join lateral jsonb_array_elements(coalesce(a->'ResourceGroup'->'Vehicle','[]'::jsonb)) as veh on true
							where (a->'ResourceGroup'->>'TypeOfResource') ilike 'L' and length(veh->>'VehicleId') >= 2
						) s
					) c
				) lc on true
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

			// Recreate previous 3-key MV without class/power columns
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
	}
}

