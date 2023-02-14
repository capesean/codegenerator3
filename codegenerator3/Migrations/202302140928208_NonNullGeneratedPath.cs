namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class NonNullGeneratedPath : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Projects", "GeneratedPath", c => c.String(nullable: false, maxLength: 50));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Projects", "GeneratedPath", c => c.String(maxLength: 50));
        }
    }
}
