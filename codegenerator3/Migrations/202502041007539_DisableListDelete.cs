namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DisableListDelete : DbMigration
    {
        public override void Up()
        {
            //AddColumn("dbo.Relationships", "DisableListDelete", c => c.Boolean(nullable: false));
            //DropColumn("dbo.Relationships", "DisableDelete");
            RenameColumn("dbo.Relationships", "DisableDelete", "DisableListDelete");
        }
        
        public override void Down()
        {
            //AddColumn("dbo.Relationships", "DisableDelete", c => c.Boolean(nullable: false));
            //DropColumn("dbo.Relationships", "DisableListDelete");
            RenameColumn("dbo.Relationships", "DisableListDelete", "DisableDelete");
        }
    }
}
