
import argon2 from 'argon2';

/** 
nyers jelszó -> Argon2id titkosított szöveg izé
@param {string} password - Nyers jelszó (stringbe mint ha pythonba definiálnád milyen adatot kér be.)
@returns {Promise<string>} - Titikosítot jelszó amit majd vissza ad (Szintén string)
 */
export async function hashPassword(password) {  //Itt lopom be a titkosításba a hülye jelszavad
  try {
    const hash = await argon2.hash(password, {
      type: argon2.argon2id, // Beálitod az Argon2id mint hash algoritmus
      memoryCost: 2 ** 15,   // 32 MB memoriát használ (2^15-en)
      timeCost: 4,           // 4 timecost (4x fut le)
      parallelism: 1         // 1 Cpu szállat használ
    });
    return hash;
  } catch (err) {
    throw new Error('Nem sikerült titkosítani a jelszót: ' + err.message);//Error
  }
}
/*
/**
 * Jelszó ellenörzés login-ból kéne bekérni a cuccost
 * @param {string} hash - ebbe fog majd beküldeni a másik js a zsamóból a hash-elt jelszót
 * @param {string} password - sima jelszó a loginból
 * @returns {Promise<boolean>}
export async function verifyPassword(hash, password) {
  try {
    return await argon2.verify(hash, password);
  } catch (err) {
    return false;
  }
}
*/