BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043158_AddComplaintRatingAndPriority'
)
BEGIN
    ALTER TABLE [Complaints] ADD [Priority] nvarchar(30) NOT NULL DEFAULT N'Bình thường';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043158_AddComplaintRatingAndPriority'
)
BEGIN
    ALTER TABLE [Complaints] ADD [RatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043158_AddComplaintRatingAndPriority'
)
BEGIN
    ALTER TABLE [Complaints] ADD [Rating] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043158_AddComplaintRatingAndPriority'
)
BEGIN
    ALTER TABLE [Complaints] ADD [ResidentFeedback] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043158_AddComplaintRatingAndPriority'
)
BEGIN
    EXEC(N'ALTER TABLE [Complaints] ADD CONSTRAINT [CK_Complaints_Priority] CHECK ([Priority] IN (N''Thấp'', N''Bình thường'', N''Cao'', N''Khẩn cấp''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043158_AddComplaintRatingAndPriority'
)
BEGIN
    EXEC(N'ALTER TABLE [Complaints] ADD CONSTRAINT [CK_Complaints_Rating] CHECK ([Rating] IS NULL OR ([Rating] >= 1 AND [Rating] <= 5))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043158_AddComplaintRatingAndPriority'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102043158_AddComplaintRatingAndPriority', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103035407_AddComplaintStatusHistoryAndHandler'
)
BEGIN
    ALTER TABLE [Complaints] ADD [HandlerUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103035407_AddComplaintStatusHistoryAndHandler'
)
BEGIN
    CREATE TABLE [ComplaintStatusHistories] (
        [ComplaintStatusHistoryId] int NOT NULL IDENTITY,
        [ComplaintId] int NOT NULL,
        [PreviousStatus] nvarchar(50) NULL,
        [NewStatus] nvarchar(50) NOT NULL,
        [ChangedByUserId] nvarchar(450) NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [Note] nvarchar(1000) NULL,
        [IsInternal] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_ComplaintStatusHistories] PRIMARY KEY ([ComplaintStatusHistoryId]),
        CONSTRAINT [FK_ComplaintStatusHistories_AspNetUsers_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ComplaintStatusHistories_Complaints_ComplaintId] FOREIGN KEY ([ComplaintId]) REFERENCES [Complaints] ([ComplaintId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103035407_AddComplaintStatusHistoryAndHandler'
)
BEGIN
    CREATE INDEX [IX_Complaints_HandlerUserId] ON [Complaints] ([HandlerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103035407_AddComplaintStatusHistoryAndHandler'
)
BEGIN
    CREATE INDEX [IX_ComplaintStatusHistories_ChangedByUserId] ON [ComplaintStatusHistories] ([ChangedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103035407_AddComplaintStatusHistoryAndHandler'
)
BEGIN
    CREATE INDEX [IX_ComplaintStatusHistories_ComplaintId_ChangedAt] ON [ComplaintStatusHistories] ([ComplaintId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103035407_AddComplaintStatusHistoryAndHandler'
)
BEGIN
    ALTER TABLE [Complaints] ADD CONSTRAINT [FK_Complaints_AspNetUsers_HandlerUserId] FOREIGN KEY ([HandlerUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103035407_AddComplaintStatusHistoryAndHandler'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103035407_AddComplaintStatusHistoryAndHandler', N'10.0.12');
END;

COMMIT;
GO

