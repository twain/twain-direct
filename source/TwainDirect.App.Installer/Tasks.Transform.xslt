<xsl:stylesheet version="1.0"
            xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:msxsl="urn:schemas-microsoft-com:xslt"
            exclude-result-prefixes="msxsl"
            xmlns:wix="http://schemas.microsoft.com/wix/2006/wi"
            xmlns:my="my:my">

   <!-- Identity transform -->
   <xsl:template match="@* | node()">
      <xsl:copy>
         <xsl:apply-templates select="@* | node()"/>
      </xsl:copy>
   </xsl:template>

   <xsl:template match="wix:Wix">
     <xsl:copy>
       <xsl:text disable-output-escaping="yes">&lt;?include Details.wxi ?&gt;</xsl:text>
       <xsl:apply-templates/>
     </xsl:copy>
   </xsl:template>

</xsl:stylesheet>