BEGIN TRANSACTION;

UPDATE Users 
SET 
    Username = 'Violet',
    FullName = 'Dr. Viloet Nkwali',
    Contact = 'violet.Nkwali@bothouniversity.com'
WHERE UserID = 5;

BEGIN TRANSACTION;

UPDATE Users 
SET 
    Username = 'admin',
    FullName = 'Amelia Behle',
    Contact = 'admin@bothouniversity.ac.bw'
WHERE UserID = 1;

BEGIN TRANSACTION;

UPDATE Users 
SET 
    Username = 'Moeketsi',
    FullName = 'Nako Moeketsi',
    Contact = 'nakomoeketsi@gmail.com'
WHERE UserID = 7;

BEGIN TRANSACTION;

UPDATE Users 
SET 
    Username = 'Ally',
    FullName = 'Ally Maphakisa',
    Contact = 'refiloe.maphakisa@bothouniversity.com'
WHERE UserID = 4;

BEGIN TRANSACTION;

UPDATE Users 
SET 
    Username = 'Palesa',
    FullName = 'Palesa Tjokolibane',
    Contact = 'palesa.tjokolibane@bothouniversity.com'
WHERE UserID = 2;

BEGIN TRANSACTION;

UPDATE Users 
SET 
    Username = 'Mathato',
    FullName = 'RN. Mathato',
    Contact = 'nursemathato@bothouniversity.com'
WHERE UserID = 3;

UPDATE Users 
SET 
    Username = 'Moliehi',
    FullName = 'Nurse Moliehi Matee',
    Contact = 'moliehi.matee@bothouniversity.com'
WHERE UserID = 6;

UPDATE Users SET RoleID = 1 WHERE Username = 'Amelia Behle';
UPDATE Users SET RoleID = 3 WHERE Username = 'Palesa';
UPDATE Users SET RoleID = 2 WHERE Username = 'Mathato';
UPDATE Users SET RoleID = 2 WHERE Username = 'Violet';
UPDATE Users SET RoleID = 2 WHERE Username = 'Moeketsi';
UPDATE Users SET RoleID = 2 WHERE Username = 'Moliehi';

COMMIT TRANSACTION;

SELECT * FROM users;

ALTER TABLE patients
ADD 
    Weight DECIMAL(4,1),        
    Height DECIMAL(3,2),        
    HeartRate INT,
    EmergencyContact VARCHAR(25); 
SELECT * FROM patients;

SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    NUMERIC_PRECISION, 
    NUMERIC_SCALE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME IN ('Patients', 'Consultations') 
AND COLUMN_NAME IN ('Weight', 'Height');

ALTER TABLE Patients ALTER COLUMN Weight DECIMAL(5,2);
ALTER TABLE Patients ALTER COLUMN Height DECIMAL(5,2);
ALTER TABLE Consultations ALTER COLUMN Weight DECIMAL(5,2);
ALTER TABLE Consultations ALTER COLUMN Height DECIMAL(5,2);

UPDATE Patients 
SET 
    Weight = COALESCE(Weight, 70.0),
    Height = COALESCE(Height, 170.0),
    HeartRate = COALESCE(HeartRate, 72),
    EmergencyContact = COALESCE(EmergencyContact, 'Not provided')
WHERE Weight IS NULL OR Height IS NULL OR HeartRate IS NULL OR EmergencyContact IS NULL;

UPDATE Consultations 
SET
    HeartRate = CASE 
        WHEN ConsultationID = 1 THEN 72
        WHEN ConsultationID = 2 THEN 75
        ELSE 70
    END,
    Weight = CASE 
        WHEN ConsultationID = 1 THEN 68.5
        WHEN ConsultationID = 2 THEN 62.8
        ELSE 65.0
    END,
    Height = CASE 
        WHEN ConsultationID = 1 THEN 175.0
        WHEN ConsultationID = 2 THEN 165.0
        ELSE 170.0
    END
WHERE HeartRate IS NULL OR Weight IS NULL OR Height IS NULL;

UPDATE Consultations 
SET 
    HeartRate = CASE 
        WHEN ConsultationID = 1 THEN 72
        WHEN ConsultationID = 2 THEN 75
        ELSE 70
    END,
    Weight = CASE 
        WHEN ConsultationID = 1 THEN 68.5
        WHEN ConsultationID = 2 THEN 62.8
        ELSE NULL
    END,
    Height = CASE 
        WHEN ConsultationID = 1 THEN 175.0
        WHEN ConsultationID = 2 THEN 165.0
        ELSE NULL
    END
WHERE HeartRate IS NULL OR Weight IS NULL OR Height IS NULL;

UPDATE Patients SET Weight = 68.5, Height = 175.0, HeartRate = 72, EmergencyContact = '+266 5732 5678' WHERE PatientID = 1;
UPDATE Patients SET Weight = 75.2, Height = 180.5, HeartRate = 68, EmergencyContact = '+266 5811 6789' WHERE PatientID = 2;
UPDATE Patients SET Weight = 62.8, Height = 165.0, HeartRate = 75, EmergencyContact = '+266 6456 7890' WHERE PatientID = 3;

UPDATE Consultations SET HeartRate = 72, Weight = 68.5, Height = 175.0 WHERE ConsultationID = 1;
UPDATE Consultations SET HeartRate = 75, Weight = 62.8, Height = 165.0 WHERE ConsultationID = 2;

SELECT * FROM patients;
SELECT * FROM consultations;