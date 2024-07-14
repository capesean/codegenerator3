namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UseAzureBlobStorage : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Fields", "UseAzureBlobStorage", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Fields", "UseAzureBlobStorage");
        }
    }
}
