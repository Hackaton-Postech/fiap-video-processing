CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261125214829_Inicial') THEN
    CREATE TABLE outbox_messages (
        "Id" uuid NOT NULL,
        "Tipo" character varying(100) NOT NULL,
        "Payload" text NOT NULL,
        "CriadoEm" timestamp with time zone NOT NULL,
        "PublicadoEm" timestamp with time zone,
        "Tentativas" integer NOT NULL,
        "UltimoErro" character varying(1000),
        CONSTRAINT "PK_outbox_messages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261125214829_Inicial') THEN
    CREATE TABLE videos (
        "Id" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "NomeOriginal" character varying(255) NOT NULL,
        "ChaveOriginal" character varying(500) NOT NULL,
        "ChaveZip" character varying(500),
        "TamanhoBytes" bigint NOT NULL,
        "Status" character varying(20) NOT NULL,
        "MensagemErro" character varying(1000),
        "CriadoEm" timestamp with time zone NOT NULL,
        "AtualizadoEm" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_videos" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261125214829_Inicial') THEN
    CREATE INDEX "IX_outbox_messages_PublicadoEm" ON outbox_messages ("PublicadoEm");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261125214829_Inicial') THEN
    CREATE INDEX "IX_videos_UsuarioId_CriadoEm" ON videos ("UsuarioId", "CriadoEm");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261125214829_Inicial') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261125214829_Inicial', '8.0.11');
    END IF;
END $EF$;
COMMIT;

