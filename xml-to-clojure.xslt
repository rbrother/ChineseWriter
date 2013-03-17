<?xml version="1.0" encoding="ISO-8859-1"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:fn="http://www.w3.org/2005/xpath-functions">
    <xsl:output method="text" version="1.0" encoding="UTF-8" indent="no"/>
    
    <xsl:template match="Words">
        [
            <xsl:apply-templates select="*"/>
        ]
    </xsl:template>
    <xsl:template match="Word">
        {
            <xsl:apply-templates select="@*"/>
        }
    </xsl:template>
    
    <xsl:template match="Word/@*">
        :<xsl:value-of select="replace(name(),'_','-')"/> 
        <xsl:text> </xsl:text>
        <xsl:apply-templates select="." mode="attrvalue"/>
    </xsl:template>

    <xsl:template match="@usage_count" mode="attrvalue">
        <xsl:value-of select="."/>
    </xsl:template>
    
    <xsl:template match="@*" mode="attrvalue">
        <xsl:text>"</xsl:text>
        <xsl:value-of select="."/>
        <xsl:text>"</xsl:text>
    </xsl:template>
    
    
    
</xsl:stylesheet>
