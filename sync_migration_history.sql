-- Script to sync migration history with existing database
-- Run this in SQL Server Management Studio

USE CodeNexusDatabase;
GO

-- Insert migration history for already existing tables
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES 
    ('20260126145916_InitialCreate', '8.0.0'),
    ('20260126152121_UpdateSubjectResourceLearningPath', '8.0.0'),
    ('20260126165146_AddOtpVerification', '8.0.0'),
    ('20260127114823_AddFirstNameLastNameField', '8.0.0');
GO

-- Verify
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;
GO
