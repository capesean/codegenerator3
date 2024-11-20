namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DTOsSearchOptionsPaths : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Projects", "DTOsPath", c => c.String(maxLength: 50));
            AddColumn("dbo.Projects", "SearchOptionsPath", c => c.String(maxLength: 50));

            Sql("update projects set DTOsPath = ModelsPath + '\\DTOs', SearchOptionsPath = ModelsPath + '\\SearchOptions'");
        }
        
        public override void Down()
        {
            DropColumn("dbo.Projects", "SearchOptionsPath");
            DropColumn("dbo.Projects", "DTOsPath");
        }
    }
}
