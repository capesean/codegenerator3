namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class OmitSaveDelete : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Entities", "OmitSave", c => c.Boolean(nullable: false));
            AddColumn("dbo.Entities", "OmitDelete", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Entities", "OmitDelete");
            DropColumn("dbo.Entities", "OmitSave");
        }
    }
}
