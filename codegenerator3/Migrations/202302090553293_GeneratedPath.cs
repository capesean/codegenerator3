namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class GeneratedPath : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Projects", "GeneratedPath", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Projects", "GeneratedPath");
        }
    }
}
