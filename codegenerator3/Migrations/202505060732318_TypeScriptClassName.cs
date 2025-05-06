namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TypeScriptClassName : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Entities", "TypeScriptClassName", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Entities", "TypeScriptClassName");
        }
    }
}
