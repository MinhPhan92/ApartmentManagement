SET NOCOUNT ON;

SELECT [MigrationId] FROM [__EFMigrationsHistory] ORDER BY [MigrationId];

SELECT i.[name], i.[is_unique], i.[filter_definition]
FROM sys.indexes i
WHERE i.[name] = 'UX_ApartmentResidents_ActiveAssignment';

SELECT cc.[name], cc.[definition]
FROM sys.check_constraints cc
WHERE cc.[name] IN (
    'CK_Residents_CitizenId_Format',
    'CK_Residents_DateOfBirth_Minimum',
    'CK_ApartmentResidents_MoveOutDate');

SELECT COUNT(*) AS TotalOccupancyRecords,
       COUNT(DISTINCT [ApartmentResidentId]) AS DistinctOccupancyIds
FROM [ApartmentResidents];

SELECT [ApartmentId], [ResidentId], COUNT(*) AS ActiveDuplicates
FROM [ApartmentResidents]
WHERE [MoveOutDate] IS NULL
GROUP BY [ApartmentId], [ResidentId]
HAVING COUNT(*) > 1;

SELECT COUNT(*) AS InvalidMoveOutDates
FROM [ApartmentResidents]
WHERE [MoveOutDate] IS NOT NULL AND [MoveOutDate] < [MoveInDate];

SELECT a.[ApartmentId], a.[ApartmentCode], a.[Status],
       SUM(CASE WHEN ar.[MoveOutDate] IS NULL THEN 1 ELSE 0 END) AS ActiveResidents
FROM [Apartments] a
LEFT JOIN [ApartmentResidents] ar ON ar.[ApartmentId] = a.[ApartmentId]
GROUP BY a.[ApartmentId], a.[ApartmentCode], a.[Status]
ORDER BY a.[ApartmentId];
