namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EditPageClasses : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Fields", "EditPageClasses", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Fields", "EditPageClasses");
        }
    }
}
