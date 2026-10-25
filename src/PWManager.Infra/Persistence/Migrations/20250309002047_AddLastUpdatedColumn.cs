using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PWManager.Infra.Persistence.Migrations
{
    public partial class AddLastUpdatedColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                PRAGMA foreign_keys=off;
                                
                -- Criar uma tabela temporária com a estrutura atualizada
                CREATE TABLE User_Temp (
                    Id TEXT PRIMARY KEY,
                    Site TEXT NOT NULL,
                    Login TEXT NOT NULL,
                    Password TEXT NOT NULL,
                    CreationDate TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    LastUpdated TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP)
                );
                
                -- Copiar os dados da tabela original para a temporária
                INSERT INTO User_Temp (Id, Site, Login, Password, CreationDate, IsActive)
                SELECT Id, Site, Login, Password, CreationDate, IsActive
                FROM User;
                
                -- Atualizar LastUpdated para todos os registros existentes
                UPDATE User_Temp SET LastUpdated = CURRENT_TIMESTAMP;
                
                -- Remover a tabela original
                DROP TABLE User;
                
                -- Renomear a tabela temporária para o nome original
                ALTER TABLE User_Temp RENAME TO User;
                
                PRAGMA foreign_keys=on;
                
            ");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                PRAGMA foreign_keys=off;
                                
                -- Criar uma tabela temporária sem a coluna LastUpdated
                CREATE TABLE User_Temp (
                    Id TEXT PRIMARY KEY,
                    Site TEXT NOT NULL,
                    Login TEXT NOT NULL,
                    Password TEXT NOT NULL,
                    CreationDate TEXT NOT NULL,
                    IsActive INTEGER NOT NULL
                );
                
                -- Copiar os dados da tabela original para a temporária
                INSERT INTO User_Temp (Id, Site, Login, Password, CreationDate, IsActive)
                SELECT Id, Site, Login, Password, CreationDate, IsActive
                FROM User;
                
                -- Remover a tabela original
                DROP TABLE User;
                
                -- Renomear a tabela temporária para o nome original
                ALTER TABLE User_Temp RENAME TO User;
                
                PRAGMA foreign_keys=on;
                
            ");
        }
    }
}
