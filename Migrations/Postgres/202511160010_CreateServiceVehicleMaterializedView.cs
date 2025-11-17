using Ae.Rail.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ae.Rail.Migrations.Postgres
{
	[DbContext(typeof(PostgresDbContext))]
	[Migration("202511160010_CreateServiceVehicleMaterializedView")]
	public partial class _202511160010_CreateServiceVehicleMaterializedView : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Drop alias and MV if exist
			migrationBuilder.Sql(@"drop view if exists service_vehicle_current;");
			migrationBuilder.Sql(@"drop materialized view if exists service_vehicle_v1 cascade;");

			// Create MV keyed by service instance + vehicle
			migrationBuilder.Sql(@"
				create materialized view service_vehicle_v1 as
				select distinct on (""OperationalTrainNumber"", ""ServiceDate"", ""OriginStd"", ""VehicleId"")
				  (payload->'OperationalTrainNumberIdentifier'->>'OperationalTrainNumber') as ""OperationalTrainNumber"",
				  to_char(
					(payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,StartDate}')::timestamp
					::date,
					'YYYY-MM-DD'
				  ) as ""ServiceDate"",
				  to_char((payload#>>'{Allocation,0,TrainOriginDateTime}')::timestamp, 'HH24:MI') as ""OriginStd"",
				  -- Vehicle fields (from allocation->ResourceGroup->Vehicle[])
				  (veh->>'VehicleId') as ""VehicleId"",
				  (veh->>'SpecificType') as ""SpecificType"",
				  (veh->>'TypeOfVehicle') as ""TypeOfVehicle"",
				  nullif(veh->>'Cabs','')::int as ""NumberOfCabs"",
				  nullif(veh->>'NumberOfSeats','')::int as ""NumberOfSeats"",
				  (veh->'Length'->>'Unit') as ""LengthUnit"",
				  nullif(veh->'Length'->>'Value','')::int as ""LengthMm"",
				  nullif(veh->>'Weight','')::int as ""Weight"",
				  nullif(veh->>'MaximumSpeed','')::int as ""MaximumSpeed"",
				  (veh->>'TrainBrakeType') as ""TrainBrakeType"",
				  (veh->>'Livery') as ""Livery"",
				  (veh->>'Decor') as ""Decor"",
				  (veh->>'VehicleStatus') as ""VehicleStatus"",
				  (veh->>'RegisteredStatus') as ""RegisteredStatus"",
				  (veh->>'RegisteredCategory') as ""RegisteredCategory"",
				  nullif(veh->>'DateRegistered','')::timestamp as ""DateRegistered"",
				  nullif(veh->>'DateEnteredService','')::timestamp as ""DateEnteredService"",
				  nullif(veh->>'ResourcePosition','')::int as ""ResourcePosition"",
				  (veh->>'PlannedResourceGroup') as ""PlannedResourceGroup"",
				  (a->'ResourceGroup'->>'ResourceGroupId') as ""ResourceGroupId"",
				  (a->'ResourceGroup'->>'FleetId') as ""FleetId"",
				  (a->'ResourceGroup'->>'TypeOfResource') as ""TypeOfResource"",
				  case when (a->'ResourceGroup'->>'TypeOfResource') ilike 'L' then true else false end as ""IsLocomotive"",
				  case
					when (a->'ResourceGroup'->>'TypeOfResource') ilike 'U' then substring((a->'ResourceGroup'->>'ResourceGroupId') from 1 for 3)
					else substring((veh->>'VehicleId') from 1 for 2)
				  end as ""ClassCode"",
				  now() as ""LastUpdatedAt""
				from message_envelopes me
				join lateral jsonb_array_elements(me.payload->'Allocation') as a on true
				join lateral jsonb_array_elements(coalesce(a->'ResourceGroup'->'Vehicle','[]'::jsonb)) as veh on true
				where me.payload ? 'OperationalTrainNumberIdentifier'
				  and (me.payload#>>'{TrainOperationalIdentification,TransportOperationalIdentifiers,0,StartDate}') is not null
				order by ""OperationalTrainNumber"", ""ServiceDate"", ""OriginStd"", ""VehicleId"", me.received_at desc;
			");

			migrationBuilder.Sql(@"create unique index if not exists ux_service_vehicle_v1_key on service_vehicle_v1 (""OperationalTrainNumber"",""ServiceDate"",""OriginStd"",""VehicleId"");");
			migrationBuilder.Sql(@"create index if not exists ix_service_vehicle_v1_service on service_vehicle_v1 (""OperationalTrainNumber"",""ServiceDate"",""OriginStd"");");

			// Stable alias
			migrationBuilder.Sql(@"create or replace view service_vehicle_current as select * from service_vehicle_v1;");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(@"drop view if exists service_vehicle_current;");
			migrationBuilder.Sql(@"drop materialized view if exists service_vehicle_v1 cascade;");
		}
	}
}


