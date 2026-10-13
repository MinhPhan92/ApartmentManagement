/*
  ApartmentHub Phase 2 migration preflight
  Target migration: 20261013014431_VehicleParkingCardsPhase2

  READ-ONLY CONTRACT
  - This script returns diagnostics only.
  - It does not repair, archive, lock, or migrate data.
  - An empty blocking result set is a necessary condition, not deployment approval.
*/

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

-- 1. Migration position and required object presence.
SELECT
    N'MigrationHistory' AS [CheckName],
    [MigrationId]
FROM [__EFMigrationsHistory]
ORDER BY [MigrationId];

SELECT
    N'RequiredObjects' AS [CheckName],
    required.[ObjectName],
    CASE WHEN OBJECT_ID(required.[ObjectName], 'U') IS NULL THEN 0 ELSE 1 END AS [Exists]
FROM (VALUES
    (N'dbo.Vehicles'),
    (N'dbo.ApartmentResidents'),
    (N'dbo.Apartments'),
    (N'dbo.IncidentTickets'),
    (N'dbo.IncidentTicketStatusHistory')
) required([ObjectName]);

-- 2. Ticket volume to reconcile with the separately approved archive.
SELECT N'IncidentTickets' AS [TableName], COUNT_BIG(*) AS [RowCount]
FROM [dbo].[IncidentTickets]
UNION ALL
SELECT N'IncidentTicketStatusHistory', COUNT_BIG(*)
FROM [dbo].[IncidentTicketStatusHistory];

-- 3. BLOCKER: vehicles for residents with no occupancy history.
SELECT
    N'VEHICLE_WITHOUT_OCCUPANCY' AS [FindingCode],
    v.[VehicleId],
    v.[ResidentId],
    v.[LicensePlate],
    v.[CreatedAt]
FROM [dbo].[Vehicles] v
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[ApartmentResidents] ar
    WHERE ar.[ResidentId] = v.[ResidentId]
)
ORDER BY v.[VehicleId];

-- 4. REVIEW: the apartment selected by the current migration algorithm.
SELECT
    N'PROPOSED_CURRENT_ALGORITHM_MAPPING' AS [FindingCode],
    v.[VehicleId],
    v.[ResidentId],
    v.[LicensePlate],
    v.[CreatedAt] AS [VehicleCreatedAt],
    proposed.[ApartmentResidentId],
    proposed.[ApartmentId],
    proposed.[MoveInDate],
    proposed.[MoveOutDate],
    CASE WHEN proposed.[MoveOutDate] IS NULL THEN N'ACTIVE_OCCUPANCY' ELSE N'MOST_RECENT_HISTORY' END AS [SelectionReason]
FROM [dbo].[Vehicles] v
OUTER APPLY (
    SELECT TOP (1)
        ar.[ApartmentResidentId], ar.[ApartmentId], ar.[MoveInDate], ar.[MoveOutDate]
    FROM [dbo].[ApartmentResidents] ar
    WHERE ar.[ResidentId] = v.[ResidentId]
    ORDER BY CASE WHEN ar.[MoveOutDate] IS NULL THEN 0 ELSE 1 END,
             ar.[MoveInDate] DESC,
             ar.[ApartmentResidentId] DESC
) proposed
ORDER BY v.[VehicleId];

-- 5. BLOCKER/REVIEW: zero or multiple occupancies valid at vehicle creation time.
SELECT
    CASE
        WHEN COUNT(ar.[ApartmentResidentId]) = 0 THEN N'NO_OCCUPANCY_AT_VEHICLE_CREATION'
        ELSE N'MULTIPLE_OCCUPANCIES_AT_VEHICLE_CREATION'
    END AS [FindingCode],
    v.[VehicleId],
    v.[ResidentId],
    v.[LicensePlate],
    v.[CreatedAt],
    COUNT(ar.[ApartmentResidentId]) AS [CandidateCount],
    STRING_AGG(CONVERT(nvarchar(max), ar.[ApartmentId]), N',') WITHIN GROUP (ORDER BY ar.[ApartmentId]) AS [CandidateApartmentIds]
FROM [dbo].[Vehicles] v
LEFT JOIN [dbo].[ApartmentResidents] ar
    ON ar.[ResidentId] = v.[ResidentId]
   AND ar.[MoveInDate] <= v.[CreatedAt]
   AND (ar.[MoveOutDate] IS NULL OR ar.[MoveOutDate] >= v.[CreatedAt])
GROUP BY v.[VehicleId], v.[ResidentId], v.[LicensePlate], v.[CreatedAt]
HAVING COUNT(ar.[ApartmentResidentId]) <> 1
ORDER BY v.[VehicleId];

-- 6. REVIEW: residents occupying more than one apartment now.
SELECT
    N'MULTIPLE_ACTIVE_APARTMENTS' AS [FindingCode],
    v.[VehicleId],
    v.[ResidentId],
    v.[LicensePlate],
    COUNT(DISTINCT ar.[ApartmentId]) AS [ActiveApartmentCount],
    STRING_AGG(CONVERT(nvarchar(max), ar.[ApartmentId]), N',') WITHIN GROUP (ORDER BY ar.[ApartmentId]) AS [ActiveApartmentIds]
FROM [dbo].[Vehicles] v
INNER JOIN [dbo].[ApartmentResidents] ar
    ON ar.[ResidentId] = v.[ResidentId]
   AND ar.[MoveOutDate] IS NULL
GROUP BY v.[VehicleId], v.[ResidentId], v.[LicensePlate]
HAVING COUNT(DISTINCT ar.[ApartmentId]) > 1
ORDER BY v.[VehicleId];

-- 7. BLOCKER: collisions produced by the exact normalization expression in Up().
WITH normalized AS (
    SELECT
        v.[VehicleId],
        v.[LicensePlate],
        UPPER(REPLACE(LTRIM(RTRIM(v.[LicensePlate])), N' ', N'')) AS [NormalizedLicensePlate]
    FROM [dbo].[Vehicles] v
), collisions AS (
    SELECT [NormalizedLicensePlate]
    FROM normalized
    GROUP BY [NormalizedLicensePlate]
    HAVING COUNT(*) > 1
)
SELECT
    N'NORMALIZED_LICENSE_PLATE_COLLISION' AS [FindingCode],
    n.[NormalizedLicensePlate],
    n.[VehicleId],
    n.[LicensePlate]
FROM normalized n
INNER JOIN collisions c ON c.[NormalizedLicensePlate] = n.[NormalizedLicensePlate]
ORDER BY n.[NormalizedLicensePlate], n.[VehicleId];

-- 8. REVIEW: characters not changed by migration SQL but treated as whitespace by clients.
SELECT
    N'NON_ASCII_OR_CONTROL_WHITESPACE' AS [FindingCode],
    v.[VehicleId],
    v.[LicensePlate]
FROM [dbo].[Vehicles] v
WHERE v.[LicensePlate] LIKE N'%' + NCHAR(9) + N'%'
   OR v.[LicensePlate] LIKE N'%' + NCHAR(10) + N'%'
   OR v.[LicensePlate] LIKE N'%' + NCHAR(13) + N'%'
   OR v.[LicensePlate] LIKE N'%' + NCHAR(160) + N'%'
ORDER BY v.[VehicleId];

-- 9. REVIEW/BLOCKER: all foreign keys involving ticket tables.
SELECT
    N'TICKET_FOREIGN_KEY_DEPENDENCY' AS [FindingCode],
    fk.[name] AS [DependencyName],
    OBJECT_SCHEMA_NAME(fk.[parent_object_id]) AS [ReferencingSchema],
    OBJECT_NAME(fk.[parent_object_id]) AS [ReferencingObject],
    OBJECT_SCHEMA_NAME(fk.[referenced_object_id]) AS [ReferencedSchema],
    OBJECT_NAME(fk.[referenced_object_id]) AS [ReferencedObject],
    fk.[delete_referential_action_desc] AS [DeleteAction],
    CASE WHEN fk.[name] IN (
        N'FK_IncidentTickets_Apartments_ApartmentId',
        N'FK_IncidentTickets_AspNetUsers_AssignedToUserId',
        N'FK_IncidentTickets_Residents_ResidentId',
        N'FK_IncidentTicketStatusHistory_AspNetUsers_ChangedByUserId',
        N'FK_IncidentTicketStatusHistory_IncidentTickets_IncidentTicketId'
    ) THEN 1 ELSE 0 END AS [KnownToEfModel]
FROM sys.foreign_keys fk
WHERE fk.[parent_object_id] IN (OBJECT_ID(N'dbo.IncidentTickets'), OBJECT_ID(N'dbo.IncidentTicketStatusHistory'))
   OR fk.[referenced_object_id] IN (OBJECT_ID(N'dbo.IncidentTickets'), OBJECT_ID(N'dbo.IncidentTicketStatusHistory'))
ORDER BY [KnownToEfModel], [DependencyName];

-- 10. REVIEW/BLOCKER: schema-bound and SQL-expression dependencies outside the ticket tables.
SELECT DISTINCT
    N'TICKET_SQL_EXPRESSION_DEPENDENCY' AS [FindingCode],
    OBJECT_SCHEMA_NAME(d.[referencing_id]) AS [ReferencingSchema],
    OBJECT_NAME(d.[referencing_id]) AS [ReferencingObject],
    o.[type_desc] AS [ReferencingType],
    d.[referenced_schema_name] AS [ReferencedSchema],
    d.[referenced_entity_name] AS [ReferencedObject],
    d.[is_schema_bound_reference] AS [IsSchemaBound]
FROM sys.sql_expression_dependencies d
LEFT JOIN sys.objects o ON o.[object_id] = d.[referencing_id]
WHERE d.[referenced_id] IN (OBJECT_ID(N'dbo.IncidentTickets'), OBJECT_ID(N'dbo.IncidentTicketStatusHistory'))
   OR (d.[referenced_schema_name] = N'dbo' AND d.[referenced_entity_name] IN (N'IncidentTickets', N'IncidentTicketStatusHistory'))
ORDER BY [ReferencingSchema], [ReferencingObject];

-- 11. REVIEW/BLOCKER: triggers attached to ticket tables.
SELECT
    N'TICKET_TRIGGER_DEPENDENCY' AS [FindingCode],
    OBJECT_SCHEMA_NAME(t.[parent_id]) AS [ParentSchema],
    OBJECT_NAME(t.[parent_id]) AS [ParentObject],
    t.[name] AS [TriggerName],
    t.[is_disabled] AS [IsDisabled]
FROM sys.triggers t
WHERE t.[parent_id] IN (OBJECT_ID(N'dbo.IncidentTickets'), OBJECT_ID(N'dbo.IncidentTicketStatusHistory'))
ORDER BY [ParentSchema], [ParentObject], [TriggerName];
