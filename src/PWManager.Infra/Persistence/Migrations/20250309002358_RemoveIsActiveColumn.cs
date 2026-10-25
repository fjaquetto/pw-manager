using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PWManager.Infra.Persistence.Migrations
{
    public partial class RemoveIsActiveColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                PRAGMA foreign_keys=off;
                                
                -- Criar uma tabela temporária sem a coluna IsActive
                CREATE TABLE User_Temp (
                    Id TEXT PRIMARY KEY,
                    Site TEXT NOT NULL,
                    Login TEXT NOT NULL,
                    Password TEXT NOT NULL,
                    CreationDate TEXT NOT NULL,
                    LastUpdated TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP)
                );
                
                -- Copiar os dados da tabela original para a temporária
                INSERT INTO User_Temp (Id, Site, Login, Password, CreationDate, LastUpdated)
                SELECT Id, Site, Login, Password, CreationDate, LastUpdated
                FROM User;
                
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
                
                BEGIN TRANSACTION;
                
                -- Criar uma tabela temporária com a coluna IsActive
                CREATE TABLE User_Temp (
                    Id TEXT PRIMARY KEY,
                    Site TEXT NOT NULL,
                    Login TEXT NOT NULL,
                    Password TEXT NOT NULL,
                    CreationDate TEXT NOT NULL,
                    LastUpdated TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                    IsActive INTEGER NOT NULL DEFAULT 1
                );
                
                -- Copiar os dados da tabela original para a temporária
                INSERT INTO User_Temp (Id, Site, Login, Password, CreationDate, LastUpdated)
                SELECT Id, Site, Login, Password, CreationDate, LastUpdated
                FROM User;
                
                -- Remover a tabela original
                DROP TABLE User;
                
                -- Renomear a tabela temporária para o nome original
                ALTER TABLE User_Temp RENAME TO User;
                
                PRAGMA foreign_keys=on;
                
                COMMIT;
            ");
        }
    }
}
