const express = require('express');
const app = express()
const path = require("path");
const mysql = require("mysql2");

app.use("/css", express.static(path.join(__dirname, "../css")));
app.use("/js", express.static(path.join(__dirname, "../js")));
app.use("/html", express.static(path.join(__dirname, "../html")));
app.use("/images", express.static(path.join(__dirname, "../images")));

app.use(express.static("vsc"));

const db = mysql.createConnection({
  host: "localhost",
  user: "root",
  password: "",
  database: "zsamo"
});


app.get("/", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/index.html"));
});

app.get("/index", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/index.html"));
});

app.get("/subs", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/subs.html"));
});
app.get("/login", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/login.html"));
});
app.get("/register", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/register.html"));
});
app.get("/gyik", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/gyik.html"));00                                                                                                                                                                                                                                                                  
});


app.get("/api/subjects", (req, res) => {

    const sql = "SELECT * FROM subjects"; 

    db.query(sql, (err, results) => {

        if (err) {
            console.error("❌ SQL hiba:", err);

            return res.status(500).json({
                error: "Adatbázis hiba"
            });
        }
 
        console.log("✅ Tantárgyak lekérve:", results.length);

        res.json(results);
    });
});


db.connect((err) => {
  if (err) {
    console.error("Kapcsolódási hiba:", err);
    return;
  }

  console.log("Sikeresen csatlakozva a MySQL-hez!");
});

app.get("/api/subjects", async (req, res) => {

    try {

        const [rows] = await db.query(`
            SELECT id, name, category
            FROM subjects
            ORDER BY name ASC
        `);

        res.json(rows);

    } catch (error) {

        console.error("Hiba:", error);

        res.status(500).json({
            error: "Nem sikerült lekérni a subjecteket."
        });

    }

});

app.listen(3000, () => console.log('Listening on port 3000...'));  
