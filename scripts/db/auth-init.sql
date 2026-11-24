CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261124001145_Inicial') THEN
    CREATE TABLE usuarios (
        "Id" uuid NOT NULL,
        "Nome" character varying(200) NOT NULL,
        "Email" character varying(254) NOT NULL,
        "SenhaHash" character varying(100) NOT NULL,
        "CriadoEm" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_usuarios" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261124001145_Inicial') THEN
    CREATE UNIQUE INDEX "IX_usuarios_Email" ON usuarios ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261124001145_Inicial') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261124001145_Inicial', '8.0.11');
    END IF;
END $EF$;
COMMIT;

